using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CinemaBooking.Domain;

public static class Roles
{
    public const string Customer = "CUSTOMER", Staff = "CINEMA_STAFF", Checker = "TICKET_CHECKER", Admin = "ADMIN", Manager = "CINEMA_MANAGER";
    public static readonly string[] All = [Customer, Staff, Checker, Admin, Manager];
}
public enum MovieStatus { NOW_SHOWING, COMING_SOON, ARCHIVED }
public enum SeatStatus { AVAILABLE, HELD, SOLD }
public enum SeatType { STANDARD, VIP }
public enum BookingStatus { CREATED, SEAT_HELD, PAYMENT_PENDING, PAID, TICKET_ISSUED, CANCELLED, EXPIRED }
public enum PaymentStatus { PENDING, SUCCESS, FAILED, TIMEOUT, REFUND_REQUIRED, REFUNDED }
public enum TicketStatus { VALID, USED, CANCELLED }

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(100)] public string FullName { get; set; } = "";
    public int? CinemaClusterId { get; set; }
    public CinemaCluster? CinemaCluster { get; set; }
    public bool IsActive { get; set; } = true;
}
public class CinemaCluster
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Required, MaxLength(80)] public string City { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<Cinema> Cinemas { get; set; } = [];
}
public class Cinema
{
    public int Id { get; set; }
    public int CinemaClusterId { get; set; }
    public CinemaCluster CinemaCluster { get; set; } = null!;
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Required, MaxLength(250)] public string Address { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<ScreeningRoom> Rooms { get; set; } = [];
}
public class ScreeningRoom
{
    public int Id { get; set; }
    public int CinemaId { get; set; }
    public Cinema Cinema { get; set; } = null!;
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [Range(1, 26)] public int RowCount { get; set; } = 8;
    [Range(1, 24)] public int ColumnCount { get; set; } = 12;
    public bool IsActive { get; set; } = true;
    public List<Seat> Seats { get; set; } = [];
}
public class Seat
{
    public int Id { get; set; }
    public int ScreeningRoomId { get; set; }
    public ScreeningRoom ScreeningRoom { get; set; } = null!;
    [MaxLength(2)] public string Row { get; set; } = "A";
    public int Number { get; set; }
    public SeatType Type { get; set; }
    public bool IsActive { get; set; } = true;
    public string Label => $"{Row}{Number}";
}
public class Movie
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(100)] public string Genre { get; set; } = "";
    [Range(30, 300)] public int DurationMinutes { get; set; }
    [Required, MaxLength(4000)] public string Description { get; set; } = "";
    [Required, MaxLength(250)] public string PosterUrl { get; set; } = "/images/orbit.svg";
    [MaxLength(10)] public string AgeRating { get; set; } = "T13";
    public DateTime ReleaseDate { get; set; }
    public MovieStatus Status { get; set; }
    public List<Showtime> Showtimes { get; set; } = [];
}
public class Showtime
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public int ScreeningRoomId { get; set; }
    public ScreeningRoom ScreeningRoom { get; set; } = null!;
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public decimal StandardPrice { get; set; }
    public decimal VipPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ShowtimeSeat> Seats { get; set; } = [];
}
public class ShowtimeSeat
{
    public int Id { get; set; }
    public int ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public int SeatId { get; set; }
    public Seat Seat { get; set; } = null!;
    public SeatStatus Status { get; set; }
    public Guid? HoldBookingId { get; set; }
    public Booking? HoldBooking { get; set; }
    public DateTime? HoldExpiresAtUtc { get; set; }
    public decimal Price { get; set; }
}
public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(40)] public string Code { get; set; } = $"VG{Guid.NewGuid():N}".ToUpperInvariant();
    [Required] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public BookingStatus Status { get; set; } = BookingStatus.CREATED;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public decimal Total { get; set; }
    public List<BookingDetail> Details { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}
public class BookingDetail
{
    public int Id { get; set; }
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public int ShowtimeSeatId { get; set; }
    public ShowtimeSeat ShowtimeSeat { get; set; } = null!;
    public decimal Price { get; set; }
    public Ticket? Ticket { get; set; }
}
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    [MaxLength(64)] public string TransactionCode { get; set; } = Guid.NewGuid().ToString("N");
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    [MaxLength(2000)] public string? GatewayReceiptJson { get; set; }
    public DateTime? ReceiptReceivedAtUtc { get; set; }
}
public class Ticket
{
    public int Id { get; set; }
    [MaxLength(64)] public string Code { get; set; } = $"VEGA-{Guid.NewGuid():N}".ToUpperInvariant();
    public int BookingDetailId { get; set; }
    public BookingDetail BookingDetail { get; set; } = null!;
    public TicketStatus Status { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}
public class TransactionLog
{
    public long Id { get; set; }
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    [MaxLength(64)] public string Event { get; set; } = "";
    [MaxLength(500)] public string Message { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public record GatewayRequest(Guid PaymentId, Guid BookingId, string TransactionCode, decimal Amount);
public record GatewayReceipt(Guid PaymentId, Guid BookingId, string TransactionCode, decimal Amount, PaymentStatus Status, string Signature);
public interface IPaymentGateway
{
    Task<GatewayReceipt> PayAsync(GatewayRequest request, string scenario, CancellationToken cancellationToken = default);
    bool Verify(GatewayReceipt receipt);
}
public interface IQrCode { byte[] CreatePng(string value); }
public class BusinessException(string message) : Exception(message);
public static class VietnamTime
{
    public static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7);
    public static DateTime Utc(DateTime local) => DateTime.SpecifyKind(local.AddHours(-7), DateTimeKind.Utc);
    public static string Money(decimal amount) => amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
}
