using CinemaBooking.Business.Services;
using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CinemaBooking.Tests;

[Collection("mysql")]
public class BookingIntegrationTests(MySqlFixture f)
{
    [Fact] public async Task TC01_BookingIssuesExactlyOneTicketPerSeat()
    {
        using var scope = f.Provider.CreateScope(); var s = scope.ServiceProvider;
        var booking = await s.GetRequiredService<BookingService>().HoldAsync(f.UserId, f.ShowtimeId, [f.SeatIds[0], f.SeatIds[1]]);
        Assert.Equal(170000m, booking.Total);
        Assert.Equal(BookingStatus.TICKET_ISSUED, await s.GetRequiredService<PaymentService>().PayAsync(booking.Id, f.UserId, "SUCCESS"));
        var db = s.GetRequiredService<VegaDbContext>();
        Assert.Equal(2, await db.Tickets.CountAsync(x => x.BookingDetail.BookingId == booking.Id));
        Assert.Equal(2, await db.ShowtimeSeats.CountAsync(x => x.HoldBookingId == booking.Id && x.Status == SeatStatus.SOLD));
    }
    [Fact] public async Task TC02_OneHundredIndependentRequestsHaveExactlyOneWinner()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 100).Select(async _ =>
        {
            using var scope = f.Provider.CreateScope(); await gate.Task;
            try { await scope.ServiceProvider.GetRequiredService<BookingService>().HoldAsync(f.UserId, f.ShowtimeId, [f.SeatIds[2]]); return true; }
            catch (BusinessException) { return false; }
        }).ToArray(); gate.SetResult();
        var outcomes = await Task.WhenAll(tasks); Assert.Equal(1, outcomes.Count(x => x));
        using var check = f.Provider.CreateScope(); var db = check.ServiceProvider.GetRequiredService<VegaDbContext>();
        Assert.Equal(1, await db.BookingDetails.CountAsync(x => x.ShowtimeSeat.SeatId == f.SeatIds[2]));
    }
    [Fact] public async Task TC03_ExpiryReleasesOnlyOwnedSeats()
    {
        using var scope = f.Provider.CreateScope(); var s = scope.ServiceProvider;
        var b = await s.GetRequiredService<BookingService>().HoldAsync(f.UserId, f.ShowtimeId, [f.SeatIds[3]]);
        var original = f.Clock.Now; f.Clock.Now = original.AddMinutes(11);
        try
        {
            await s.GetRequiredService<SeatHoldService>().ExpireAsync(); var db = s.GetRequiredService<VegaDbContext>();
            Assert.Equal(BookingStatus.EXPIRED, await db.Bookings.Where(x => x.Id == b.Id).Select(x => x.Status).SingleAsync());
            Assert.Equal(SeatStatus.AVAILABLE, await db.ShowtimeSeats.Where(x => x.ShowtimeId == f.ShowtimeId && x.SeatId == f.SeatIds[3]).Select(x => x.Status).SingleAsync());
        } finally { f.Clock.Now = original; }
    }
    [Fact] public async Task TC04_FailedPaymentHasNoTicketsAndReleasesSeats()
    {
        using var scope = f.Provider.CreateScope(); var s = scope.ServiceProvider; var b = await s.GetRequiredService<BookingService>().HoldAsync(f.UserId, f.ShowtimeId, [f.SeatIds[4]]);
        Assert.Equal(BookingStatus.CANCELLED, await s.GetRequiredService<PaymentService>().PayAsync(b.Id, f.UserId, "FAILED"));
        var db = s.GetRequiredService<VegaDbContext>(); Assert.False(await db.Tickets.AnyAsync(x => x.BookingDetail.BookingId == b.Id));
        Assert.Equal(SeatStatus.AVAILABLE, await db.ShowtimeSeats.Where(x => x.ShowtimeId == f.ShowtimeId && x.SeatId == f.SeatIds[4]).Select(x => x.Status).SingleAsync());
    }
    [Fact] public async Task TC05_ConcurrentDuplicateCallbacksAreIdempotent()
    {
        Guid id; GatewayReceipt receipt;
        using (var scope = f.Provider.CreateScope())
        {
            var s = scope.ServiceProvider; var b = await s.GetRequiredService<BookingService>().HoldAsync(f.UserId, f.ShowtimeId, [f.SeatIds[5]]); id = b.Id;
            var request = await s.GetRequiredService<PaymentService>().PrepareAsync(id, f.UserId);
            receipt = await s.GetRequiredService<IPaymentGateway>().PayAsync(request, "SUCCESS");
        }
        await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ => { using var scope = f.Provider.CreateScope(); await scope.ServiceProvider.GetRequiredService<PaymentService>().ConfirmAsync(receipt); }));
        using var check = f.Provider.CreateScope(); var db = check.ServiceProvider.GetRequiredService<VegaDbContext>();
        Assert.Equal(1, await db.Tickets.CountAsync(x => x.BookingDetail.BookingId == id)); Assert.Equal(1, await db.Payments.CountAsync(x => x.BookingId == id && x.Status == PaymentStatus.SUCCESS));
    }
    [Fact] public async Task TC06_LateSuccessCannotTakeAnotherBookingsSeat()
    {
        Guid oldId, newId; GatewayReceipt receipt; var original = f.Clock.Now;
        try
        {
            using (var scope = f.Provider.CreateScope())
            {
                var s = scope.ServiceProvider; var b = await s.GetRequiredService<BookingService>().HoldAsync(f.UserId, f.ShowtimeId, [f.SeatIds[6]]); oldId = b.Id;
                receipt = await s.GetRequiredService<IPaymentGateway>().PayAsync(await s.GetRequiredService<PaymentService>().PrepareAsync(oldId, f.UserId), "SUCCESS");
            }
            f.Clock.Now = original.AddMinutes(11);
            using (var scope = f.Provider.CreateScope())
            {
                var s = scope.ServiceProvider; await s.GetRequiredService<SeatHoldService>().ExpireAsync();
                var b = await s.GetRequiredService<BookingService>().HoldAsync(f.OtherUserId, f.ShowtimeId, [f.SeatIds[6]]); newId = b.Id;
                await s.GetRequiredService<PaymentService>().PayAsync(newId, f.OtherUserId, "SUCCESS");
            }
            using (var scope = f.Provider.CreateScope())
            {
                var s = scope.ServiceProvider; await s.GetRequiredService<PaymentService>().ConfirmAsync(receipt); var db = s.GetRequiredService<VegaDbContext>();
                Assert.Equal(newId, await db.ShowtimeSeats.Where(x => x.ShowtimeId == f.ShowtimeId && x.SeatId == f.SeatIds[6]).Select(x => x.HoldBookingId).SingleAsync());
                Assert.Equal(PaymentStatus.REFUND_REQUIRED, await db.Payments.Where(x => x.BookingId == oldId).Select(x => x.Status).SingleAsync());
                Assert.False(await db.Tickets.AnyAsync(x => x.BookingDetail.BookingId == oldId));
            }
        } finally { f.Clock.Now = original; }
    }
    [Fact] public async Task TC07_TicketCanBeUsedOnlyOnce()
    {
        using var scope = f.Provider.CreateScope(); var s = scope.ServiceProvider; var b = await s.GetRequiredService<BookingService>().HoldAsync(f.UserId, f.ShowtimeId, [f.SeatIds[7]]);
        await s.GetRequiredService<PaymentService>().PayAsync(b.Id, f.UserId, "SUCCESS");
        var code = await s.GetRequiredService<VegaDbContext>().Tickets.Where(x => x.BookingDetail.BookingId == b.Id).Select(x => x.Code).SingleAsync();
        await s.GetRequiredService<TicketService>().UseAsync(code, null, true);
        await Assert.ThrowsAsync<BusinessException>(() => s.GetRequiredService<TicketService>().UseAsync(code, null, true));
    }
    [Fact] public async Task TC09_ConcurrentOverlappingShowtimesAreRejected()
    {
        using var scope = f.Provider.CreateScope(); var s = scope.ServiceProvider; var existing = await s.GetRequiredService<VegaDbContext>().Showtimes.AsNoTracking().SingleAsync(x => x.Id == f.ShowtimeId);
        await Assert.ThrowsAsync<BusinessException>(() => s.GetRequiredService<ShowtimeService>().SaveAsync(new Showtime { MovieId = existing.MovieId, ScreeningRoomId = existing.ScreeningRoomId, StartsAtUtc = existing.StartsAtUtc.AddMinutes(30), StandardPrice = 85000, VipPrice = 110000 }));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 2).Select(async _ => { using var request = f.Provider.CreateScope(); await gate.Task;
            try { await request.ServiceProvider.GetRequiredService<ShowtimeService>().SaveAsync(new Showtime { MovieId = existing.MovieId, ScreeningRoomId = existing.ScreeningRoomId, StartsAtUtc = existing.StartsAtUtc.AddDays(1), StandardPrice = 85000, VipPrice = 110000 }); return true; } catch (BusinessException) { return false; } }).ToArray();
        gate.SetResult(); Assert.Equal(1, (await Task.WhenAll(tasks)).Count(x => x));
    }
    [Fact] public async Task AllOrNothingHoldRollsBackUncontestedSeat()
    {
        using (var scope = f.Provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<BookingService>().HoldAsync(f.UserId, f.ShowtimeId, [f.SeatIds[9]]);
        using (var scope = f.Provider.CreateScope()) await Assert.ThrowsAsync<BusinessException>(() => scope.ServiceProvider.GetRequiredService<BookingService>().HoldAsync(f.OtherUserId, f.ShowtimeId, [f.SeatIds[8], f.SeatIds[9]]));
        using var check = f.Provider.CreateScope(); Assert.Equal(SeatStatus.AVAILABLE, await check.ServiceProvider.GetRequiredService<VegaDbContext>().ShowtimeSeats.Where(x => x.ShowtimeId == f.ShowtimeId && x.SeatId == f.SeatIds[8]).Select(x => x.Status).SingleAsync());
    }
    [Fact] public async Task ForgedReceiptAndWrongAmountCannotIssueTickets()
    {
        using var scope = f.Provider.CreateScope(); var s = scope.ServiceProvider; var b = await s.GetRequiredService<BookingService>().HoldAsync(f.UserId, f.ShowtimeId, [f.SeatIds[10]]);
        var request = await s.GetRequiredService<PaymentService>().PrepareAsync(b.Id, f.UserId); var receipt = await s.GetRequiredService<IPaymentGateway>().PayAsync(request, "SUCCESS");
        await Assert.ThrowsAsync<BusinessException>(() => s.GetRequiredService<PaymentService>().ConfirmAsync(receipt with { Amount = receipt.Amount + 1 }));
        var signedWrong = await s.GetRequiredService<IPaymentGateway>().PayAsync(request with { Amount = 1 }, "SUCCESS");
        await Assert.ThrowsAsync<BusinessException>(() => s.GetRequiredService<PaymentService>().ConfirmAsync(signedWrong));
    }
}
