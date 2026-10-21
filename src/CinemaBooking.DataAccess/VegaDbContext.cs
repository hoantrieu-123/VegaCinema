using CinemaBooking.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CinemaBooking.DataAccess;

public class VegaDbContext(DbContextOptions<VegaDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<CinemaCluster> CinemaClusters => Set<CinemaCluster>();
    public DbSet<Cinema> Cinemas => Set<Cinema>();
    public DbSet<ScreeningRoom> ScreeningRooms => Set<ScreeningRoom>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<ShowtimeSeat> ShowtimeSeats => Set<ShowtimeSeat>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingDetail> BookingDetails => Set<BookingDetail>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TransactionLog> TransactionLogs => Set<TransactionLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.HasCharSet("utf8mb4");
        b.Entity<ShowtimeSeat>().HasIndex(x => new { x.ShowtimeId, x.SeatId }).IsUnique();
        b.Entity<Seat>().HasIndex(x => new { x.ScreeningRoomId, x.Row, x.Number }).IsUnique();
        b.Entity<Booking>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Booking>().HasIndex(x => new { x.Status, x.ExpiresAtUtc });
        b.Entity<Booking>().HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        b.Entity<BookingDetail>().HasIndex(x => new { x.BookingId, x.ShowtimeSeatId }).IsUnique();
        b.Entity<Ticket>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Ticket>().HasOne(x => x.BookingDetail).WithOne(x => x.Ticket).HasForeignKey<Ticket>(x => x.BookingDetailId);
        b.Entity<Payment>().HasIndex(x => x.TransactionCode).IsUnique();
        b.Entity<Showtime>().HasIndex(x => new { x.ScreeningRoomId, x.StartsAtUtc, x.EndsAtUtc });
        b.Entity<Showtime>().HasIndex(x => new { x.MovieId, x.StartsAtUtc });
        b.Entity<ShowtimeSeat>().HasIndex(x => new { x.HoldBookingId, x.Status });
        b.Entity<TransactionLog>().HasIndex(x => x.CreatedAtUtc);
        b.Entity<Seat>().Ignore(x => x.Label);
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(decimal)) { property.SetPrecision(14); property.SetScale(2); }
            }
            foreach (var fk in entity.GetForeignKeys()) fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
        b.Entity<Booking>().Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Entity<ShowtimeSeat>().Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Entity<Seat>().Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Entity<Payment>().Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Entity<Ticket>().Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Entity<Movie>().Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
    }
}
