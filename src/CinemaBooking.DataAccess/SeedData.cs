using CinemaBooking.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace CinemaBooking.DataAccess;
public static class SeedData
{
    public static async Task InitializeAsync(VegaDbContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, string? password, bool large = false)
    {
        foreach (var role in Roles.All) if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole(role)));
        await PosterCatalog.SynchronizeAsync(db);
        if (!await db.CinemaClusters.AnyAsync())
        {
            for (var c = 0; c < (large ? 10 : 2); c++)
            {
                var cluster = new CinemaCluster { Name = c == 0 ? "VEGA Sài Gòn" : c == 1 ? "VEGA Hà Nội" : $"VEGA Khu vực {c + 1}", City = c == 0 ? "TP. Hồ Chí Minh" : c == 1 ? "Hà Nội" : $"Thành phố demo {c + 1}" };
                db.CinemaClusters.Add(cluster);
                var cinema = new Cinema { CinemaCluster = cluster, Name = c == 0 ? "VEGA Landmark" : c == 1 ? "VEGA West Lake" : $"VEGA Central {c + 1}", Address = c == 0 ? "Tầng 5, Trung tâm Landmark, Bình Thạnh" : c == 1 ? "Tầng 4, Trung tâm West Lake, Tây Hồ" : "Trung tâm thương mại demo" };
                db.Cinemas.Add(cinema);
                for (var r = 1; r <= (large ? 10 : 2); r++)
                {
                    var room = new ScreeningRoom { Cinema = cinema, Name = $"Phòng {r:00} · Dolby 7.1" };
                    db.ScreeningRooms.Add(room);
                    for (var row = 0; row < room.RowCount; row++)
                        for (var col = 1; col <= room.ColumnCount; col++) db.Seats.Add(new Seat { ScreeningRoom = room, Row = ((char)('A' + row)).ToString(), Number = col, Type = row is >= 3 and <= 5 ? SeatType.VIP : SeatType.STANDARD });
                }
            }
            await db.SaveChangesAsync();
        }
        await PosterCatalog.EnsureUpcomingShowtimesAsync(db);
        if (!string.IsNullOrWhiteSpace(password))
        {
            var clusterId = await db.CinemaClusters.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
            foreach (var role in Roles.All)
            {
                var name = role switch { Roles.Customer => "customer", Roles.Staff => "staff", Roles.Checker => "checker", Roles.Admin => "admin", _ => "manager" };
                var email = $"{name}@vega.local";
                if (await users.FindByEmailAsync(email) is not null) continue;
                var user = new ApplicationUser { UserName = email, Email = email, FullName = $"Vega {name}", EmailConfirmed = true, CinemaClusterId = role is Roles.Staff or Roles.Checker or Roles.Manager ? clusterId : null };
                Check(await users.CreateAsync(user, password)); Check(await users.AddToRoleAsync(user, role));
            }
        }
    }
    private static void Check(IdentityResult result) { if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description))); }
}
