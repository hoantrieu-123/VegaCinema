using System.ComponentModel.DataAnnotations;
using CinemaBooking.Domain;
namespace VegaCinema.ViewModels;
public record HomePage(List<Movie> NowShowing, List<Movie> ComingSoon, List<CinemaCluster> Clusters, List<Showtime> Showtimes);
public record MoviePage(List<Movie> Movies, string? Search, string? Genre, MovieStatus? Status, int Page, int Total);
public record MovieDetails(Movie Movie, List<Showtime> Showtimes, List<CinemaCluster> Clusters, DateTime? Date, int? CinemaId);
public record SchedulePage(List<Showtime> Showtimes, List<Movie> Movies, List<CinemaCluster> Clusters, int? MovieId, int? CinemaId, DateTime Date);
public record SeatPage(Showtime Showtime, List<ShowtimeSeat> Seats);
public record CheckoutPage(Booking Booking, DateTime ServerNowUtc);
public class HoldInput { [Range(1, int.MaxValue)] public int ShowtimeId { get; set; } public int[] SeatIds { get; set; } = []; }
public class LoginInput
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
public class RegisterInput
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(100, ErrorMessage = "Họ tên không vượt quá 100 ký tự.")] public string FullName { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."), MinLength(8, ErrorMessage = "Mật khẩu cần ít nhất 8 ký tự.")] public string Password { get; set; } = "";
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")] public string ConfirmPassword { get; set; } = "";
}
public class ProfileInput
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(100, ErrorMessage = "Họ tên không vượt quá 100 ký tự.")] public string FullName { get; set; } = "";
    [StringLength(25, ErrorMessage = "Số điện thoại không vượt quá 25 ký tự."), Phone(ErrorMessage = "Số điện thoại không hợp lệ.")] public string? Phone { get; set; }
    public string Email { get; set; } = "";
}
public class PasswordInput
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")] public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới."), MinLength(8, ErrorMessage = "Mật khẩu cần ít nhất 8 ký tự.")] public string NewPassword { get; set; } = "";
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")] public string ConfirmPassword { get; set; } = "";
}
