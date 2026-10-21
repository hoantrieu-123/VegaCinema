using CinemaBooking.Business.Services;
using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using CinemaBooking.Integrations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
namespace CinemaBooking.Tests;

[CollectionDefinition("mysql", DisableParallelization = true)]
public class MySqlCollection : ICollectionFixture<MySqlFixture>;
public class MutableClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
public class MySqlFixture : IAsyncLifetime
{
    public ServiceProvider Provider { get; private set; } = null!;
    public string Connection { get; private set; } = "";
    public MutableClock Clock { get; } = new();
    public string UserId { get; private set; } = "";
    public string OtherUserId { get; private set; } = "";
    public int ShowtimeId { get; private set; }
    public int[] SeatIds { get; private set; } = [];
    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("VEGA_TEST_MYSQL") ?? throw new InvalidOperationException("Cần VEGA_TEST_MYSQL trỏ đến MySQL thật. Không dùng InMemory hoặc bỏ qua kiểm thử.");
        var cs = new MySqlConnectionStringBuilder(connection) { Database = "vega_test_" + Guid.NewGuid().ToString("N"), MaximumPoolSize = 150 };
        Connection = cs.ConnectionString;
        var services = new ServiceCollection();
        services.AddLogging(); services.AddDbContext<VegaDbContext>(o => o.UseMySql(Connection, new MySqlServerVersion(new Version(8,4,0))));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<VegaDbContext>();
        services.AddSingleton<TimeProvider>(Clock); services.Configure<BookingOptions>(o => o.HoldMinutes = 10);
        services.AddSingleton<IPaymentGateway>(new MockPaymentGateway(Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))));
        services.AddScoped<BookingService>(); services.AddScoped<PaymentService>(); services.AddScoped<SeatHoldService>(); services.AddScoped<ShowtimeService>(); services.AddScoped<TicketService>();
        Provider = services.BuildServiceProvider();
        using var scope = Provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<VegaDbContext>();
        await db.Database.MigrateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var email in new[] { "test1@vega.local", "test2@vega.local" })
        { var user = new ApplicationUser { UserName = email, Email = email, FullName = "Test customer" }; Assert.True((await userManager.CreateAsync(user, "TestOnly!7429")).Succeeded); if (UserId == "") UserId = user.Id; else OtherUserId = user.Id; }
        var cluster = new CinemaCluster { Name = "Test cluster", City = "Test city" };
        var cinema = new Cinema { CinemaCluster = cluster, Name = "Test cinema", Address = "Test address" };
        var room = new ScreeningRoom { Cinema = cinema, Name = "Test room", RowCount = 2, ColumnCount = 12 };
        var movie = new Movie { Title = "Test movie", Genre = "Test", Description = "Test movie description", DurationMinutes = 90, Status = MovieStatus.NOW_SHOWING, ReleaseDate = Clock.Now.Date };
        db.AddRange(cluster, cinema, room, movie); await db.SaveChangesAsync();
        for (var n = 1; n <= 12; n++) db.Seats.Add(new Seat { ScreeningRoomId = room.Id, Row = "A", Number = n });
        await db.SaveChangesAsync();
        ShowtimeId = await scope.ServiceProvider.GetRequiredService<ShowtimeService>().SaveAsync(new Showtime { MovieId = movie.Id, ScreeningRoomId = room.Id, StartsAtUtc = Clock.Now.AddDays(2).UtcDateTime, StandardPrice = 85000, VipPrice = 110000 });
        SeatIds = await db.Seats.OrderBy(x => x.Number).Select(x => x.Id).ToArrayAsync();
    }
    public async Task DisposeAsync()
    {
        // Only the random database created by this fixture may be removed.
        var name = new MySqlConnectionStringBuilder(Connection).Database;
        if (!name.StartsWith("vega_test_", StringComparison.Ordinal) || name.Length != 42) throw new InvalidOperationException("Refusing unsafe database cleanup.");
        using var scope = Provider.CreateScope(); await scope.ServiceProvider.GetRequiredService<VegaDbContext>().Database.EnsureDeletedAsync();
        await Provider.DisposeAsync();
    }
}
