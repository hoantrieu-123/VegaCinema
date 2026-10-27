using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CinemaBooking.Business.Services;

public class BookingOptions { public int HoldMinutes { get; set; } = 10; }
public class BookingService(VegaDbContext db, TimeProvider clock, IOptions<BookingOptions> options)
{
    public async Task<Booking> HoldAsync(string userId, int showtimeId, int[] seatIds)
    {
        if (seatIds.Length is < 1 or > 8 || seatIds.Distinct().Count() != seatIds.Length)
            throw new BusinessException("Vui lòng chọn từ 1 đến 8 ghế khác nhau.");
        var now = clock.GetUtcNow().UtcDateTime;
        var show = await db.Showtimes.AsNoTracking().Include(x => x.ScreeningRoom).ThenInclude(x => x.Cinema).ThenInclude(x => x.CinemaCluster)
            .SingleOrDefaultAsync(x => x.Id == showtimeId);
        if (show is null || !show.IsActive || show.StartsAtUtc <= now || !show.ScreeningRoom.IsActive || !show.ScreeningRoom.Cinema.IsActive || !show.ScreeningRoom.Cinema.CinemaCluster.IsActive)
            throw new BusinessException("Suất chiếu không còn nhận đặt vé.");
        var seats = await db.ShowtimeSeats.AsNoTracking().Include(x => x.Seat)
            .Where(x => x.ShowtimeId == showtimeId && seatIds.Contains(x.SeatId) && x.Seat.IsActive && x.Seat.ScreeningRoomId == show.ScreeningRoomId)
            .OrderBy(x => x.Id).ToListAsync();
        if (seats.Count != seatIds.Length) throw new BusinessException("Ghế không thuộc phòng chiếu hoặc đã ngừng sử dụng.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var booking = new Booking { UserId = userId, ShowtimeId = showtimeId, CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(Math.Clamp(options.Value.HoldMinutes, 5, 10)), Total = seats.Sum(x => x.Price) };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        foreach (var seat in seats)
        {
            var changed = await db.ShowtimeSeats.Where(x => x.Id == seat.Id &&
                    (x.Status == SeatStatus.AVAILABLE || (x.Status == SeatStatus.HELD && x.HoldExpiresAtUtc <= now)))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SeatStatus.HELD)
                    .SetProperty(x => x.HoldBookingId, (Guid?)booking.Id).SetProperty(x => x.HoldExpiresAtUtc, (DateTime?)booking.ExpiresAtUtc));
            if (changed != 1) throw new BusinessException($"Ghế {seat.Seat.Label} vừa được người khác chọn. Vui lòng chọn ghế khác.");
            db.BookingDetails.Add(new BookingDetail { BookingId = booking.Id, ShowtimeSeatId = seat.Id, Price = seat.Price });
        }
        booking.Status = BookingStatus.SEAT_HELD;
        Log(booking.Id, "SEAT_HELD", $"Đã giữ {seats.Count} ghế.");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return booking;
    }

    public IQueryable<Booking> Query() => db.Bookings.AsNoTracking().Include(x => x.User)
        .Include(x => x.Showtime).ThenInclude(x => x.Movie)
        .Include(x => x.Showtime).ThenInclude(x => x.ScreeningRoom).ThenInclude(x => x.Cinema).ThenInclude(x => x.CinemaCluster)
        .Include(x => x.Details).ThenInclude(x => x.ShowtimeSeat).ThenInclude(x => x.Seat)
        .Include(x => x.Details).ThenInclude(x => x.Ticket).Include(x => x.Payments).AsSplitQuery();

    public async Task<Booking> GetOwnedAsync(Guid id, string userId)
        => await Query().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId)
            ?? throw new BusinessException("Không tìm thấy đơn đặt vé của bạn.");

    public async Task CancelAsync(Guid id, string userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var booking = await LockAsync(id);
        if (booking is null || booking.UserId != userId) throw new BusinessException("Không tìm thấy đơn đặt vé của bạn.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (booking.Status is BookingStatus.CANCELLED or BookingStatus.EXPIRED) throw new BusinessException("Đơn đã hủy hoặc hết hạn.");
        var show = await db.Showtimes.SingleAsync(x => x.Id == booking.ShowtimeId);
        var tickets = await db.Tickets.Where(x => x.BookingDetail.BookingId == id).ToListAsync();
        if (tickets.Any(x => x.Status == TicketStatus.USED) || (booking.Status == BookingStatus.TICKET_ISSUED && show.StartsAtUtc < now.AddHours(2)))
            throw new BusinessException("Chỉ hủy vé chưa sử dụng và trước giờ chiếu ít nhất 2 giờ.");
        foreach (var ticket in tickets) ticket.Status = TicketStatus.CANCELLED;
        await db.Payments.Where(x => x.BookingId == id && x.Status == PaymentStatus.SUCCESS)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, PaymentStatus.REFUNDED));
        await ReleaseAsync(booking, BookingStatus.CANCELLED);
        Log(id, "CANCELLED", "Khách hủy đơn; hoàn tiền mô phỏng nếu đã thanh toán.");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    

internal Task<Booking?> LockAsync(Guid id)
{
    if (db.Database.IsMySql())
    {
        return db.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM `Bookings` WHERE `Id` = {id} FOR UPDATE"
            )
            .SingleOrDefaultAsync();
    }

    if (db.Database.IsSqlite())
    {
        return db.Bookings.SingleOrDefaultAsync(x => x.Id == id);
    }

    throw new NotSupportedException("Database không được hỗ trợ.");
}

    internal async Task ReleaseAsync(Booking booking, BookingStatus status)
    {
        await db.ShowtimeSeats.Where(x => x.HoldBookingId == booking.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SeatStatus.AVAILABLE)
                .SetProperty(x => x.HoldBookingId, (Guid?)null).SetProperty(x => x.HoldExpiresAtUtc, (DateTime?)null));
        booking.Status = status;
    }
    internal void Log(Guid? bookingId, string name, string message) => db.TransactionLogs.Add(new TransactionLog
        { BookingId = bookingId, Event = name, Message = message, CreatedAtUtc = clock.GetUtcNow().UtcDateTime });
}

public class SeatHoldService(VegaDbContext db, BookingService bookings, TimeProvider clock)
{
    public async Task<int> ExpireAsync()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await db.Bookings.AsNoTracking().Where(x => (x.Status == BookingStatus.SEAT_HELD || x.Status == BookingStatus.PAYMENT_PENDING)
            && x.ExpiresAtUtc <= now).OrderBy(x => x.ExpiresAtUtc).Select(x => x.Id).Take(200).ToListAsync();
        var count = 0;
        foreach (var id in ids)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var booking = await bookings.LockAsync(id);
            if (booking is not null && booking.ExpiresAtUtc <= now && booking.Status is BookingStatus.SEAT_HELD or BookingStatus.PAYMENT_PENDING)
            {
                await bookings.ReleaseAsync(booking, BookingStatus.EXPIRED);
                bookings.Log(id, "EXPIRED", "Hết thời gian giữ ghế.");
                await db.SaveChangesAsync(); count++;
            }
            await tx.CommitAsync();
            db.ChangeTracker.Clear();
        }
        return count;
    }
}
