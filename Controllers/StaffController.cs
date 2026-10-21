using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace VegaCinema.Controllers;
[Authorize(Roles = Roles.Staff + "," + Roles.Admin + "," + Roles.Checker)]
public class StaffController(BookingService bookings, TicketService tickets, UserManager<ApplicationUser> users) : Controller
{
    [Authorize(Roles = Roles.Staff + "," + Roles.Admin)] public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var user = (await users.GetUserAsync(User))!; var admin = User.IsInRole(Roles.Admin);
        var q = bookings.Query().Where(x => (admin || x.Showtime.ScreeningRoom.Cinema.CinemaClusterId == user.CinemaClusterId) && (string.IsNullOrEmpty(search) || x.Code.Contains(search) || x.User.Email!.Contains(search))).OrderByDescending(x => x.CreatedAtUtc);
        ViewBag.Search = search; ViewBag.Page = Math.Max(1, page); ViewBag.Total = await q.CountAsync();
        return View(await q.Skip((Math.Max(1, page) - 1) * 15).Take(15).ToListAsync());
    }
    [Authorize(Roles = Roles.Checker + "," + Roles.Admin)] public async Task<IActionResult> Check(string? code)
    {
        var user = (await users.GetUserAsync(User))!; ViewBag.Code = code;
        if (string.IsNullOrWhiteSpace(code)) return View();
        var ticket = await tickets.FindAsync(code.Trim(), user.CinemaClusterId, User.IsInRole(Roles.Admin));
        if (ticket is null) ViewBag.Message = "Không tìm thấy vé trong phạm vi được cấp quyền."; return View(ticket);
    }
    [HttpPost, Authorize(Roles = Roles.Checker + "," + Roles.Admin)] public async Task<IActionResult> Use(string code)
    {
        var user = (await users.GetUserAsync(User))!; await tickets.UseAsync(code, user.CinemaClusterId, User.IsInRole(Roles.Admin));
        TempData["Success"] = "Vé hợp lệ. Đã xác nhận khách vào rạp."; return RedirectToAction(nameof(Check), new { code });
    }
}
