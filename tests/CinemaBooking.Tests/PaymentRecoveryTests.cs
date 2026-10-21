using CinemaBooking.Business.Services;
using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
namespace CinemaBooking.Tests;

[Collection("mysql")]
public class PaymentRecoveryTests(MySqlFixture f)
{
    private class FailTicketIssuance : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Ticket>().Any(x => x.State == EntityState.Added)) throw new InvalidOperationException("Simulated ticket storage outage");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
    [Fact] public async Task DurableGatewayEvidenceRecoversAfterTicketStorageFailure()
    {
        GatewayReceipt receipt; Guid bookingId;
        using (var scope = f.Provider.CreateScope())
        {
            var s = scope.ServiceProvider; var db = s.GetRequiredService<VegaDbContext>();
            var original = await db.Showtimes.AsNoTracking().SingleAsync(x => x.Id == f.ShowtimeId);
            var showId = await s.GetRequiredService<ShowtimeService>().SaveAsync(new Showtime { MovieId = original.MovieId, ScreeningRoomId = original.ScreeningRoomId, StartsAtUtc = original.StartsAtUtc.AddDays(5), StandardPrice = 85000, VipPrice = 110000 });
            var booking = await s.GetRequiredService<BookingService>().HoldAsync(f.UserId, showId, [f.SeatIds[0]]); bookingId = booking.Id;
            var request = await s.GetRequiredService<PaymentService>().PrepareAsync(bookingId, f.UserId);
            receipt = await s.GetRequiredService<IPaymentGateway>().PayAsync(request, "SUCCESS");
        }
        var gateway = f.Provider.GetRequiredService<IPaymentGateway>();
        var options = new DbContextOptionsBuilder<VegaDbContext>().UseMySql(f.Connection, new MySqlServerVersion(new Version(8,4,0))).AddInterceptors(new FailTicketIssuance()).Options;
        await using (var failedDb = new VegaDbContext(options))
        {
            var bookings = new BookingService(failedDb, f.Clock, Options.Create(new BookingOptions()));
            var payment = new PaymentService(failedDb, bookings, gateway, f.Clock);
            await Assert.ThrowsAsync<InvalidOperationException>(() => payment.ConfirmAsync(receipt));
        }
        using var recovery = f.Provider.CreateScope(); var provider = recovery.ServiceProvider; var verify = provider.GetRequiredService<VegaDbContext>();
        Assert.NotNull(await verify.Payments.Where(x => x.BookingId == bookingId).Select(x => x.GatewayReceiptJson).SingleAsync());
        Assert.Equal(PaymentStatus.PENDING, await verify.Payments.Where(x => x.BookingId == bookingId).Select(x => x.Status).SingleAsync());
        Assert.False(await verify.Tickets.AnyAsync(x => x.BookingDetail.BookingId == bookingId));
        Assert.Equal(BookingStatus.TICKET_ISSUED, await provider.GetRequiredService<PaymentService>().RetryAsync(receipt.PaymentId));
        Assert.Equal(1, await verify.Tickets.CountAsync(x => x.BookingDetail.BookingId == bookingId));
    }
}
