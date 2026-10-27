using System.Security.Claims;
using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VegaCinema.ViewModels;
namespace VegaCinema.Controllers;
public class BookingController(BookingService bookings, ShowtimeService shows, SeatHoldService holds, PaymentService payments, TimeProvider clock, UserManager<ApplicationUser> users, IQrCode qr) : Controller
{
    public async Task<IActionResult> Seats(int id)
    {
        var show = await shows.Query().SingleOrDefaultAsync(x => x.Id == id); if (show is null) return NotFound();
        var user = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        var customer = user is null ? null : new CustomerContact(user.FullName, user.Email ?? "", user.PhoneNumber);
        return View(new SeatPage(show, await shows.SeatsAsync(id), customer));
    }
    [HttpGet] public async Task<IActionResult> Availability(int id)
    {
        if (!await shows.Query().AnyAsync(x => x.Id == id)) return NotFound();
        var now = clock.GetUtcNow().UtcDateTime;
        return Json(new { serverNowUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc), seats = (await shows.SeatsAsync(id)).Select(x => new { seatId = x.SeatId, price = x.Price,
            status = !x.Seat.IsActive ? "SOLD" : x.Status == SeatStatus.HELD && x.HoldExpiresAtUtc <= now ? "AVAILABLE" : x.Status.ToString() }) });
    }
    [Authorize, HttpPost] public async Task<IActionResult> Hold([FromBody] HoldInput input)
    {
        if (!ModelState.IsValid || input.SeatIds is null) return BadRequest(new { message = "Danh sách ghế không hợp lệ." });
        var booking = await bookings.HoldAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, input.ShowtimeId, input.SeatIds);
        await payments.PrepareAsync(booking.Id, booking.UserId);
        return Json(new { redirectUrl = Url.Action(nameof(Checkout), new { id = booking.Id }) });
    }
    [Authorize] public async Task<IActionResult> Checkout(Guid id)
    {
        await holds.ExpireAsync(); var booking = await bookings.GetOwnedAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (booking.Status == BookingStatus.TICKET_ISSUED) return RedirectToAction(nameof(Details), new { id });
        return View(new CheckoutPage(booking, clock.GetUtcNow().UtcDateTime));
    }
    [Authorize, HttpPost] public async Task<IActionResult> CreatePaymentQr(Guid id)
    {
        await payments.PrepareAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return RedirectToAction(nameof(Checkout), new { id });
    }
    [Authorize, HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PaymentQr(Guid id, Guid paymentId)
    {
        var booking = await bookings.GetOwnedAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!CanPay(booking, paymentId)) return NotFound();
        var url = Url.Action(nameof(DemoPayment), "Booking", new { id, paymentId }, Request.Scheme)!;
        return File(qr.CreatePng(url), "image/png");
    }
    [Authorize, HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DemoPayment(Guid id, Guid paymentId)
    {
        await holds.ExpireAsync();
        var booking = await bookings.GetOwnedAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (booking.Status == BookingStatus.TICKET_ISSUED) return RedirectToAction(nameof(Details), new { id });
        if (!CanPay(booking, paymentId)) return RedirectToAction(nameof(Checkout), new { id });
        return View(new DemoPaymentPage(booking, booking.Payments.Single(x => x.Id == paymentId), clock.GetUtcNow().UtcDateTime));
    }
    [Authorize, HttpPost] public async Task<IActionResult> Pay(Guid id, Guid paymentId, string scenario)
    {
        var booking = await bookings.GetOwnedAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (booking.Status == BookingStatus.TICKET_ISSUED) return RedirectToAction(nameof(Details), new { id });
        if (!CanPay(booking, paymentId)) throw new BusinessException("Mã QR đã hết hạn hoặc giao dịch không thuộc đơn này.");
        var status = await payments.PayAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!, scenario);
        if (status == BookingStatus.TICKET_ISSUED) TempData["Success"] = "Thanh toán QR demo thành công. Vé điện tử của bạn đã được phát hành.";
        return RedirectToAction(nameof(Details), new { id });
    }
    private bool CanPay(Booking booking, Guid paymentId) => booking.Status == BookingStatus.PAYMENT_PENDING
        && booking.ExpiresAtUtc > clock.GetUtcNow().UtcDateTime
        && booking.Payments.Any(x => x.Id == paymentId && x.Status == PaymentStatus.PENDING);
    [Authorize] public async Task<IActionResult> Details(Guid id)
    {
        var user = (await users.GetUserAsync(User))!;
        var admin = User.IsInRole(Roles.Admin); var staff = User.IsInRole(Roles.Staff);
        var booking = await bookings.Query().SingleOrDefaultAsync(x => x.Id == id && (x.UserId == user.Id || admin || (staff && x.Showtime.ScreeningRoom.Cinema.CinemaClusterId == user.CinemaClusterId)));
        if (booking is null) return NotFound(); return View(booking);
    }
    [Authorize] public async Task<IActionResult> History(int page = 1)
    {
        page = Math.Max(page, 1); var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var q = bookings.Query().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc);
        ViewBag.Page = page; ViewBag.Total = await q.CountAsync(); return View(await q.Skip((page - 1) * 10).Take(10).ToListAsync());
    }
    [Authorize, HttpPost] public async Task<IActionResult> Cancel(Guid id)
    { await bookings.CancelAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!); TempData["Success"] = "Đã hủy đơn và giải phóng ghế."; return RedirectToAction(nameof(Details), new { id }); }
}
