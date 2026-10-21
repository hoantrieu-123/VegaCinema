using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using Microsoft.EntityFrameworkCore;

namespace CinemaBooking.Business.Services;

public class MovieService(VegaDbContext db)
{
    public IQueryable<Movie> Query(string? search = null, string? genre = null, MovieStatus? status = null)
    {
        var q = db.Movies.AsNoTracking().Where(x => x.Status != MovieStatus.ARCHIVED);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => x.Title.Contains(search));
        if (!string.IsNullOrWhiteSpace(genre)) q = q.Where(x => x.Genre.Contains(genre));
        if (status.HasValue) q = q.Where(x => x.Status == status);
        return q.OrderBy(x => x.Id);
    }
    public Task<Movie?> FindAsync(int id) => Query().SingleOrDefaultAsync(x => x.Id == id);
    public async Task SaveAsync(Movie movie)
    {
        if (!movie.PosterUrl.StartsWith("/images/") || movie.PosterUrl.Contains("..")) throw new BusinessException("Poster phải nằm trong /images/ của website.");
        if (movie.Id == 0) db.Movies.Add(movie);
        else
        {
            var existing = await db.Movies.FindAsync(movie.Id) ?? throw new BusinessException("Không tìm thấy phim.");
            db.Entry(existing).CurrentValues.SetValues(movie);
        }
        await db.SaveChangesAsync();
    }
}
public class CinemaService(VegaDbContext db)
{
    public Task<List<CinemaCluster>> ListAsync() => db.CinemaClusters.AsNoTracking().Where(x => x.IsActive)
        .Include(x => x.Cinemas.Where(c => c.IsActive)).ThenInclude(x => x.Rooms.Where(r => r.IsActive)).AsSplitQuery().OrderBy(x => x.Id).ToListAsync();
}
public class ShowtimeService(VegaDbContext db, TimeProvider clock)
{
    public IQueryable<Showtime> Query(int? movieId = null, int? cinemaId = null, DateTime? date = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var q = db.Showtimes.AsNoTracking().Include(x => x.Movie).Include(x => x.ScreeningRoom).ThenInclude(x => x.Cinema).ThenInclude(x => x.CinemaCluster)
            .Where(x => x.IsActive && x.StartsAtUtc > now && x.Movie.Status == MovieStatus.NOW_SHOWING && x.ScreeningRoom.IsActive && x.ScreeningRoom.Cinema.IsActive && x.ScreeningRoom.Cinema.CinemaCluster.IsActive);
        if (movieId.HasValue) q = q.Where(x => x.MovieId == movieId);
        if (cinemaId.HasValue) q = q.Where(x => x.ScreeningRoom.CinemaId == cinemaId);
        if (date.HasValue) { var from = VietnamTime.Utc(date.Value.Date); var to = from.AddDays(1); q = q.Where(x => x.StartsAtUtc >= from && x.StartsAtUtc < to); }
        return q.OrderBy(x => x.StartsAtUtc);
    }
    public async Task<List<ShowtimeSeat>> SeatsAsync(int id) => await db.ShowtimeSeats.AsNoTracking().Include(x => x.Seat).Where(x => x.ShowtimeId == id).OrderBy(x => x.Seat.Row).ThenBy(x => x.Seat.Number).ToListAsync();
    public async Task<int> SaveAsync(Showtime show)
    {
        if (show.StandardPrice <= 0 || show.VipPrice < show.StandardPrice) throw new BusinessException("Giá vé không hợp lệ; giá VIP phải từ giá thường trở lên.");
        if (show.StartsAtUtc <= clock.GetUtcNow().UtcDateTime) throw new BusinessException("Suất chiếu phải ở tương lai.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var room = await db.ScreeningRooms.FromSqlInterpolated($"SELECT * FROM `ScreeningRooms` WHERE `Id` = {show.ScreeningRoomId} FOR UPDATE").SingleOrDefaultAsync();
        if (room is null || !room.IsActive) throw new BusinessException("Phòng chiếu không khả dụng.");
        var movie = await db.Movies.SingleOrDefaultAsync(x => x.Id == show.MovieId && x.Status != MovieStatus.ARCHIVED) ?? throw new BusinessException("Phim không hợp lệ.");
        show.EndsAtUtc = show.StartsAtUtc.AddMinutes(movie.DurationMinutes);
        var before = show.StartsAtUtc.AddMinutes(-15); var after = show.EndsAtUtc.AddMinutes(15);
        if (await db.Showtimes.AnyAsync(x => x.Id != show.Id && x.ScreeningRoomId == show.ScreeningRoomId && x.IsActive && x.StartsAtUtc < after && x.EndsAtUtc > before))
            throw new BusinessException("Phòng đã có suất chiếu trùng giờ (bao gồm 15 phút dọn phòng).");
        if (show.Id != 0)
        {
            var existing = await db.Showtimes.FindAsync(show.Id) ?? throw new BusinessException("Không tìm thấy suất chiếu.");
            if (await db.Bookings.AnyAsync(x => x.ShowtimeId == show.Id) && (existing.MovieId != show.MovieId || existing.ScreeningRoomId != show.ScreeningRoomId || existing.StartsAtUtc != show.StartsAtUtc || existing.StandardPrice != show.StandardPrice || existing.VipPrice != show.VipPrice))
                throw new BusinessException("Suất đã có đơn đặt vé; chỉ được thay đổi trạng thái nhận đặt vé.");
            if (existing.ScreeningRoomId != show.ScreeningRoomId) throw new BusinessException("Không đổi phòng của suất đã tạo. Hãy ngừng suất cũ và tạo suất mới.");
            db.Entry(existing).CurrentValues.SetValues(show);
            await db.ShowtimeSeats.Where(x => x.ShowtimeId == show.Id && x.Seat.Type == SeatType.STANDARD).ExecuteUpdateAsync(s => s.SetProperty(x => x.Price, show.StandardPrice));
            await db.ShowtimeSeats.Where(x => x.ShowtimeId == show.Id && x.Seat.Type == SeatType.VIP).ExecuteUpdateAsync(s => s.SetProperty(x => x.Price, show.VipPrice));
        }
        else
        {
            db.Showtimes.Add(show); await db.SaveChangesAsync();
            var seats = await db.Seats.Where(x => x.ScreeningRoomId == room.Id && x.IsActive).ToListAsync();
            if (seats.Count == 0) throw new BusinessException("Phòng chưa có ghế.");
            db.ShowtimeSeats.AddRange(seats.Select(s => new ShowtimeSeat { ShowtimeId = show.Id, SeatId = s.Id, Price = s.Type == SeatType.VIP ? show.VipPrice : show.StandardPrice }));
        }
        await db.SaveChangesAsync(); await tx.CommitAsync(); return show.Id;
    }
}
