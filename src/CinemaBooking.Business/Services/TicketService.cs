using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using Microsoft.EntityFrameworkCore;

namespace CinemaBooking.Business.Services;

public class TicketService(VegaDbContext db, BookingService bookings, TimeProvider clock)
{
    public IQueryable<Ticket> Query() => db.Tickets.AsNoTracking().Include(x => x.BookingDetail).ThenInclude(x => x.ShowtimeSeat).ThenInclude(x => x.Seat)
        .Include(x => x.BookingDetail).ThenInclude(x => x.Booking).ThenInclude(x => x.Showtime).ThenInclude(x => x.Movie)
        .Include(x => x.BookingDetail).ThenInclude(x => x.Booking).ThenInclude(x => x.Showtime).ThenInclude(x => x.ScreeningRoom).ThenInclude(x => x.Cinema);
    public async Task<Ticket?> FindAsync(string code, int? clusterId, bool admin)
        => await Query().SingleOrDefaultAsync(x => x.Code == code && (admin || x.BookingDetail.Booking.Showtime.ScreeningRoom.Cinema.CinemaClusterId == clusterId));
    public async Task UseAsync(string code, int? clusterId, bool admin)
    {
        var ticket = await FindAsync(code, clusterId, admin) ?? throw new BusinessException("Không tìm thấy vé trong phạm vi được cấp quyền.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var booking = await bookings.LockAsync(ticket.BookingDetail.BookingId);
        if (booking?.Status != BookingStatus.TICKET_ISSUED || ticket.BookingDetail.Booking.Showtime.EndsAtUtc < clock.GetUtcNow().UtcDateTime)
            throw new BusinessException("Vé đã hủy, hết hiệu lực hoặc đơn chưa được xác nhận.");
        var changed = await db.Tickets.Where(x => x.Id == ticket.Id && x.Status == TicketStatus.VALID)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, TicketStatus.USED).SetProperty(x => x.UsedAtUtc, (DateTime?)clock.GetUtcNow().UtcDateTime));
        if (changed != 1) throw new BusinessException("Vé đã được sử dụng hoặc không hợp lệ.");
        bookings.Log(booking.Id, "TICKET_USED", $"Đã kiểm soát vé #{ticket.Id}.");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
