using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using Microsoft.EntityFrameworkCore;
namespace CinemaBooking.Business.Services;
public record ReportGroup(string Name, int Tickets, decimal Revenue);
public record ReportResult(DateTime From, DateTime To, int Tickets, int Transactions, decimal Revenue, List<ReportGroup> Movies, List<ReportGroup> Cinemas, List<ReportGroup> Showtimes);
public class ReportService(VegaDbContext db)
{
    public async Task<ReportResult> GetAsync(DateTime from, DateTime to, int? clusterId, bool admin)
    {
        if (to < from || (to - from).TotalDays > 366) throw new BusinessException("Chọn khoảng thời gian hợp lệ, tối đa 366 ngày.");
        var start = VietnamTime.Utc(from.Date); var end = VietnamTime.Utc(to.Date.AddDays(1));
        var details = await db.BookingDetails.AsNoTracking().Where(x => x.Booking.CreatedAtUtc >= start && x.Booking.CreatedAtUtc < end
            && x.Booking.Status == BookingStatus.TICKET_ISSUED && (admin || x.Booking.Showtime.ScreeningRoom.Cinema.CinemaClusterId == clusterId))
            .Select(x => new { x.Price, x.BookingId, Movie = x.Booking.Showtime.Movie.Title, Cinema = x.Booking.Showtime.ScreeningRoom.Cinema.Name, Show = x.Booking.Showtime.Id, x.Booking.Showtime.StartsAtUtc }).ToListAsync();
        return new(from, to, details.Count, details.Select(x => x.BookingId).Distinct().Count(), details.Sum(x => x.Price),
            details.GroupBy(x => x.Movie).Select(g => new ReportGroup(g.Key, g.Count(), g.Sum(x => x.Price))).ToList(),
            details.GroupBy(x => x.Cinema).Select(g => new ReportGroup(g.Key, g.Count(), g.Sum(x => x.Price))).ToList(),
            details.GroupBy(x => new { x.Show, x.StartsAtUtc }).Select(g => new ReportGroup($"#{g.Key.Show} · {VietnamTime.Local(g.Key.StartsAtUtc):dd/MM HH:mm}", g.Count(), g.Sum(x => x.Price))).ToList());
    }
}
