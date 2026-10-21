using System.Text.Json;
using CinemaBooking.Domain;
using Microsoft.EntityFrameworkCore;

namespace CinemaBooking.DataAccess;

public static class PosterCatalog
{
    public record PosterEntry(string Title, string PosterUrl);

    public static IReadOnlyList<PosterEntry> Read()
    {
        using var stream = typeof(PosterCatalog).Assembly.GetManifestResourceStream("CinemaBooking.DataAccess.SeedData.movie-posters.json")
            ?? throw new InvalidOperationException("Không tìm thấy danh mục poster phim.");
        return JsonSerializer.Deserialize<List<PosterEntry>>(stream)
            ?? throw new InvalidOperationException("Danh mục poster phim không hợp lệ.");
    }

    // Retain the original sample entities for existing bookings and electronic tickets.
    private static readonly Dictionary<string, string> OriginalSamples = new()
    {
        ["QUỸ ĐẠO CUỐI CÙNG"] = "/images/orbit.svg",
        ["THÀNH PHỐ SAU CƠN MƯA"] = "/images/rain.svg",
        ["MẬT MÃ BÓNG ĐÊM"] = "/images/night.svg",
        ["KHU RỪNG KỲ DIỆU"] = "/images/forest.svg",
        ["CHẠM VÀO MÙA HẠ"] = "/images/summer.svg",
        ["ĐƯỜNG ĐUA VÔ TẬN"] = "/images/race.svg",
        ["HÀNH TINH XANH"] = "/images/planet.svg",
        ["NGƯỜI GIỮ NHỮNG VÌ SAO"] = "/images/stars.svg"
    };

    public static async Task SynchronizeAsync(VegaDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var existing = await db.Movies.ToListAsync();
        var today = VietnamTime.Local(DateTime.UtcNow).Date;
        foreach (var entry in Read())
        {
            var movie = existing.FirstOrDefault(x => x.PosterUrl == entry.PosterUrl || string.Equals(x.Title, entry.Title, StringComparison.OrdinalIgnoreCase));
            if (movie is not null)
            {
                // Keep any metadata and status already edited by an administrator.
                movie.PosterUrl = entry.PosterUrl;
                continue;
            }
            movie = new Movie
            {
                Title = entry.Title, PosterUrl = entry.PosterUrl, Genre = "Phim điện ảnh", DurationMinutes = 120,
                AgeRating = "Chưa rõ", ReleaseDate = today, Status = MovieStatus.NOW_SHOWING,
                Description = "Poster được cung cấp trong bộ sưu tập Phim. Nội dung chi tiết đang được cập nhật. "
                    + "Thời lượng, ngày khởi chiếu và lịch chiếu hiện là dữ liệu demo phục vụ trải nghiệm đặt vé."
            };
            db.Movies.Add(movie);
        }
        var originalIds = existing.Where(x => OriginalSamples.TryGetValue(x.Title, out var poster) && x.PosterUrl == poster).Select(x => x.Id).ToArray();
        foreach (var movie in existing.Where(x => originalIds.Contains(x.Id))) movie.Status = MovieStatus.ARCHIVED;
        var now = DateTime.UtcNow;
        await db.Showtimes.Where(x => originalIds.Contains(x.MovieId) && x.StartsAtUtc > now && !db.Bookings.Any(b => b.ShowtimeId == x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public static async Task EnsureUpcomingShowtimesAsync(VegaDbContext db)
    {
        var urls = Read().Select(x => x.PosterUrl).ToArray();
        var movies = await db.Movies.AsNoTracking().Where(x => urls.Contains(x.PosterUrl) && x.Status == MovieStatus.NOW_SHOWING).OrderBy(x => x.Id).ToListAsync();
        if (movies.Count == 0) return;
        var rooms = await db.ScreeningRooms.AsNoTracking().Where(x => x.IsActive && x.Cinema.IsActive && x.Cinema.CinemaCluster.IsActive)
            .Include(x => x.Seats.Where(s => s.IsActive)).OrderBy(x => x.Id).ToListAsync();
        var now = DateTime.UtcNow;
        var today = VietnamTime.Local(now).Date;
        for (var roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            var room = rooms[roomIndex];
            if (room.Seats.Count == 0) continue;
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.ScreeningRooms.FromSqlInterpolated($"SELECT * FROM `ScreeningRooms` WHERE `Id` = {room.Id} FOR UPDATE").SingleAsync();
            var scheduled = await db.Showtimes.AsNoTracking().Where(x => x.ScreeningRoomId == room.Id && x.IsActive && x.EndsAtUtc > now)
                .Select(x => new { x.StartsAtUtc, x.EndsAtUtc }).ToListAsync();
            for (var day = 0; day < 7; day++)
                for (var slot = 0; slot < 5; slot++)
                {
                    var starts = VietnamTime.Utc(today.AddDays(day).AddHours(9 + slot * 3));
                    if (starts <= now) continue;
                    var movie = movies[(day * rooms.Count * 5 + roomIndex * 5 + slot) % movies.Count];
                    var ends = starts.AddMinutes(movie.DurationMinutes);
                    if (scheduled.Any(x => x.StartsAtUtc < ends.AddMinutes(15) && x.EndsAtUtc > starts.AddMinutes(-15))) continue;
                    var show = new Showtime { MovieId = movie.Id, ScreeningRoomId = room.Id, StartsAtUtc = starts, EndsAtUtc = ends, StandardPrice = 85000, VipPrice = 110000 };
                    foreach (var seat in room.Seats) show.Seats.Add(new ShowtimeSeat { SeatId = seat.Id, Price = seat.Type == SeatType.VIP ? show.VipPrice : show.StandardPrice });
                    db.Showtimes.Add(show);
                    scheduled.Add(new { StartsAtUtc = starts, EndsAtUtc = ends });
                }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
    }
}
