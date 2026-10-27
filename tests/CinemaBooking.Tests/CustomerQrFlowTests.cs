using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CinemaBooking.Business.Services;
using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CinemaBooking.Tests;

[Collection("mysql")]
public class CustomerQrFlowTests(MySqlFixture f)
{
    private WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Testing");
        b.UseSetting("Database:Provider", "MySql");
        b.UseSetting("Database:AutoMigrate", "false");
        b.UseSetting("Database:SeedDemo", "false");
        b.UseSetting("ConnectionStrings:VegaDatabase", f.Connection);
        b.UseSetting("Payment:SigningKey", "TEST-ONLY-" + Guid.NewGuid().ToString("N"));
    });

    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    private static Dictionary<string, string> Registration(string email, string token) => new()
    {
        ["FullName"] = "Khách hàng QR", ["BirthYear"] = "2000", ["Gender"] = "Female",
        ["Email"] = email, ["Address"] = "123 Nguyễn Trãi, TP. Hồ Chí Minh", ["Phone"] = "0901234567",
        ["Password"] = "QrTestOnly!7429", ["ConfirmPassword"] = "QrTestOnly!7429", ["__RequestVerificationToken"] = token
    };

    [Fact]
    public async Task RegistrationContactAndQrPaymentIssueTicketsOnlyAfterConfirmation()
    {
        int showtimeId;
        using (var scope = f.Provider.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(Roles.Customer)) Assert.True((await roles.CreateAsync(new IdentityRole(Roles.Customer))).Succeeded);
            var original = await scope.ServiceProvider.GetRequiredService<VegaDbContext>().Showtimes.AsNoTracking().SingleAsync(x => x.Id == f.ShowtimeId);
            showtimeId = await scope.ServiceProvider.GetRequiredService<ShowtimeService>().SaveAsync(new Showtime
            {
                MovieId = original.MovieId, ScreeningRoomId = original.ScreeningRoomId,
                StartsAtUtc = f.Clock.Now.AddDays(10).UtcDateTime, StandardPrice = 85000, VipPrice = 110000
            });
        }
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var email = "qr-" + Guid.NewGuid().ToString("N") + "@vega.local";
        var token = Token(await client.GetStringAsync("/Account/Register"));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Register", new FormUrlEncodedContent(Registration(email, token)))).StatusCode);
        using (var scope = f.Provider.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<VegaDbContext>().Users.SingleAsync(x => x.Email == email);
            Assert.Equal("Khách hàng QR", user.FullName); Assert.Equal(2000, user.BirthYear);
            Assert.Equal(Gender.Female, user.Gender); Assert.Equal("0901234567", user.PhoneNumber);
            Assert.Equal("123 Nguyễn Trãi, TP. Hồ Chí Minh", user.Address);
        }
        var seats = await client.GetStringAsync("/Booking/Seats/" + showtimeId);
        var decodedSeats = WebUtility.HtmlDecode(seats);
        Assert.Contains("Khách hàng QR", decodedSeats); Assert.Contains(email, decodedSeats); Assert.Contains("0901234567", decodedSeats);
        token = Token(seats);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        var held = await client.PostAsJsonAsync("/Booking/Hold", new { showtimeId, seatIds = new[] { f.SeatIds[0] } });
        Assert.Equal(HttpStatusCode.OK, held.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var checkoutUrl = JsonDocument.Parse(await held.Content.ReadAsStringAsync()).RootElement.GetProperty("redirectUrl").GetString()!;
        var bookingId = Guid.Parse(checkoutUrl.Split('/').Last());
        Guid paymentId;
        using (var scope = f.Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VegaDbContext>();
            Assert.False(await db.Tickets.AnyAsync(x => x.BookingDetail.BookingId == bookingId));
            paymentId = await db.Payments.Where(x => x.BookingId == bookingId && x.Status == PaymentStatus.PENDING).Select(x => x.Id).SingleAsync();
        }
        var checkout = WebUtility.HtmlDecode(await client.GetStringAsync(checkoutUrl));
        Assert.Contains("id=\"payment-qr\"", checkout); Assert.Contains(email, checkout); Assert.Contains("0901234567", checkout);
        var qr = await client.GetAsync($"/Booking/PaymentQr/{bookingId}?paymentId={paymentId}");
        Assert.Equal("image/png", qr.Content.Headers.ContentType!.MediaType);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, (await qr.Content.ReadAsByteArrayAsync()).Take(8).ToArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Booking/PaymentQr/{bookingId}?paymentId={Guid.NewGuid()}")).StatusCode);
        using (var stranger = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") }))
        {
            var loginToken = Token(await stranger.GetStringAsync("/Account/Login"));
            Assert.Equal(HttpStatusCode.Redirect, (await stranger.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Email"] = "test2@vega.local", ["Password"] = "TestOnly!7429", ["__RequestVerificationToken"] = loginToken }))).StatusCode);
            stranger.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            Assert.Equal(HttpStatusCode.Conflict, (await stranger.GetAsync($"/Booking/PaymentQr/{bookingId}?paymentId={paymentId}")).StatusCode);
        }
        var demo = await client.GetStringAsync($"/Booking/DemoPayment/{bookingId}?paymentId={paymentId}");
        var confirm = new Dictionary<string, string> { ["id"] = bookingId.ToString(), ["paymentId"] = paymentId.ToString(), ["scenario"] = "SUCCESS", ["__RequestVerificationToken"] = Token(demo) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Booking/Pay", new FormUrlEncodedContent(confirm.Where(x => x.Key != "__RequestVerificationToken"))
        )).StatusCode); // A form token is required even if the QR URL is known.
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Booking/Pay", new FormUrlEncodedContent(confirm))).StatusCode);
        var details = await client.GetStringAsync("/Booking/Details/" + bookingId);
        Assert.Contains("class=\"e-ticket\"", details);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Booking/Pay", new FormUrlEncodedContent(confirm))).StatusCode);
        using var verify = f.Provider.CreateScope();
        var final = verify.ServiceProvider.GetRequiredService<VegaDbContext>();
        Assert.Equal(1, await final.Tickets.CountAsync(x => x.BookingDetail.BookingId == bookingId));
        Assert.Equal(PaymentStatus.SUCCESS, await final.Payments.Where(x => x.Id == paymentId).Select(x => x.Status).SingleAsync());
    }

    [Theory]
    [InlineData("BirthYear", "2999")]
    [InlineData("Gender", "999")]
    [InlineData("Phone", "not-a-phone")]
    [InlineData("Address", "")]
    public async Task InvalidProfileDoesNotCreateAnAccount(string field, string value)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var email = "invalid-" + Guid.NewGuid().ToString("N") + "@vega.local";
        var input = Registration(email, Token(await client.GetStringAsync("/Account/Register")));
        input[field] = value;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Account/Register", new FormUrlEncodedContent(input))).StatusCode);
        using var scope = f.Provider.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<VegaDbContext>().Users.AnyAsync(x => x.Email == email));
    }
}
