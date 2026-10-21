using System.Net;
using System.Text.RegularExpressions;
using CinemaBooking.Business.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CinemaBooking.Tests;

[Collection("mysql")]
public class AuthorizationTests(MySqlFixture f)
{
    [Fact] public async Task FiveWrongPasswordsLockTheAccountForFifteenMinutes()
    {
        var email = "lockout-" + Guid.NewGuid().ToString("N") + "@vega.local";
        string userId;
        using (var scope = f.Provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<CinemaBooking.Domain.ApplicationUser>>();
            var user = new CinemaBooking.Domain.ApplicationUser { UserName = email, Email = email, FullName = "Lockout test" };
            Assert.True((await users.CreateAsync(user, "LockoutTest!7429")).Succeeded); userId = user.Id;
        }
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing"); b.UseSetting("Payment:SigningKey", "TEST-ONLY-" + Guid.NewGuid().ToString("N"));
            b.UseSetting("ConnectionStrings:VegaDatabase", f.Connection);
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        for (var n = 0; n < 5; n++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["Email"] = email, ["Password"] = "WrongPassword!7429", ["__RequestVerificationToken"] = token }))).StatusCode);
        using var verify = f.Provider.CreateScope();
        var manager = verify.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<CinemaBooking.Domain.ApplicationUser>>();
        var locked = (await manager.FindByIdAsync(userId))!;
        Assert.True(await manager.IsLockedOutAsync(locked));
        Assert.True(locked.LockoutEnd >= DateTimeOffset.UtcNow.AddMinutes(14));
    }
    [Fact] public async Task TC08_RealIdentityRejectsAdminAccessAndOtherCustomersBooking()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing"); b.UseSetting("Payment:SigningKey", "TEST-ONLY-" + Guid.NewGuid().ToString("N")); b.UseSetting("ConnectionStrings:VegaDatabase", f.Connection);
            b.UseSetting("Database:AutoMigrate", "false"); b.UseSetting("Database:SeedDemo", "false");
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var result = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["Email"] = "test1@vega.local", ["Password"] = "TestOnly!7429", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var denied = await client.GetAsync("/Admin"); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/Account/Denied", denied.Headers.Location!.OriginalString);
        Guid bookingId;
        using (var scope = f.Provider.CreateScope()) bookingId = (await scope.ServiceProvider.GetRequiredService<BookingService>().HoldAsync(f.OtherUserId, f.ShowtimeId, [f.SeatIds[11]])).Id;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Booking/Details/" + bookingId)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Booking/Hold", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }
    [Theory]
    [InlineData("/")][InlineData("/Movies")][InlineData("/Schedule")][InlineData("/Cinemas")][InlineData("/Account/Register")]
    public async Task PublicRazorPagesRenderAgainstMySql(string path)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => { b.UseEnvironment("Testing"); b.UseSetting("Payment:SigningKey", "TEST-ONLY-" + Guid.NewGuid().ToString("N")); b.UseSetting("ConnectionStrings:VegaDatabase", f.Connection); });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("VEGA", await response.Content.ReadAsStringAsync());
    }
}
