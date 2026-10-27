using System.ComponentModel.DataAnnotations;
using CinemaBooking.Domain;
namespace VegaCinema.ViewModels;
public record HomePage(List<Movie> NowShowing, List<Movie> ComingSoon, List<CinemaCluster> Clusters, List<Showtime> Showtimes);
public record MoviePage(List<Movie> Movies, string? Search, string? Genre, MovieStatus? Status, int Page, int Total);
public record MovieDetails(Movie Movie, List<Showtime> Showtimes, List<CinemaCluster> Clusters, DateTime? Date, int? CinemaId);
public record SchedulePage(List<Showtime> Showtimes, List<Movie> Movies, List<CinemaCluster> Clusters, int? MovieId, int? CinemaId, DateTime Date);
public record CustomerContact(string FullName, string Email, string? Phone);
public record SeatPage(Showtime Showtime, List<ShowtimeSeat> Seats, CustomerContact? Customer = null);
public record CheckoutPage(Booking Booking, DateTime ServerNowUtc);
public record DemoPaymentPage(Booking Booking, Payment Payment, DateTime ServerNowUtc);
public class HoldInput { [Range(1, int.MaxValue)] public int ShowtimeId { get; set; } public int[] SeatIds { get; set; } = []; }
public class LoginInput
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
public class CustomerProfileInput : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(100, ErrorMessage = "Họ tên không vượt quá 100 ký tự.")] public string FullName { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập năm sinh."), Range(1900, 9999, ErrorMessage = "Năm sinh phải từ 1900 đến năm hiện tại.")] public int? BirthYear { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn giới tính."), EnumDataType(typeof(Gender), ErrorMessage = "Giới tính không hợp lệ.")] public Gender? Gender { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ."), StringLength(250, ErrorMessage = "Địa chỉ không vượt quá 250 ký tự.")] public string Address { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại."), StringLength(25), Phone(ErrorMessage = "Số điện thoại không hợp lệ."), RegularExpression(@"^\+?[0-9][0-9 .()\-]{6,24}$", ErrorMessage = "Số điện thoại không hợp lệ.")] public string Phone { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BirthYear > VietnamTime.Local(DateTime.UtcNow).Year)
            yield return new ValidationResult("Năm sinh không được lớn hơn năm hiện tại.", [nameof(BirthYear)]);
    }
}
public class RegisterInput : CustomerProfileInput
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."), MinLength(8, ErrorMessage = "Mật khẩu cần ít nhất 8 ký tự.")] public string Password { get; set; } = "";
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")] public string ConfirmPassword { get; set; } = "";
}
public class ProfileInput : CustomerProfileInput
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string Email { get; set; } = "";
}
public class PasswordInput
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")] public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới."), MinLength(8, ErrorMessage = "Mật khẩu cần ít nhất 8 ký tự.")] public string NewPassword { get; set; } = "";
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")] public string ConfirmPassword { get; set; } = "";
}
