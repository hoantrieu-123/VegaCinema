using System.Security.Claims;
using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VegaCinema.ViewModels;
namespace VegaCinema.Controllers;
public class BookingController(BookingService bookings, ShowtimeService shows, SeatHoldService holds, PaymentService payments, TimeProvider clock, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Seats(int id)
    {
        var show = await shows.Query().SingleOrDefaultAsync(x => x.Id == id); if (show is null) return NotFound();
        return View(new SeatPage(show, await shows.SeatsAsync(id)));
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
        return Json(new { redirectUrl = Url.Action(nameof(Checkout), new { id = booking.Id }) });
    }
    [Authorize] public async Task<IActionResult> Checkout(Guid id)
    {
        await holds.ExpireAsync(); var booking = await bookings.GetOwnedAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (booking.Status == BookingStatus.TICKET_ISSUED) return RedirectToAction(nameof(Details), new { id });
        return View(new CheckoutPage(booking, clock.GetUtcNow().UtcDateTime));
    }
    [Authorize, HttpPost] public async Task<IActionResult> Pay(Guid id, string scenario)
    {
        await payments.PayAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!, scenario);
        return RedirectToAction(nameof(Details), new { id });
    }
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
