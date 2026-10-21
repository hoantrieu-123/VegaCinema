using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
namespace VegaCinema.Areas.Admin.Controllers;
[Area("Admin"), Authorize(Roles = Roles.Admin + "," + Roles.Manager)]
public class ReportsController(ReportService reports, UserManager<ApplicationUser> users, TimeProvider clock) : Controller
{
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    { var now = VietnamTime.Local(clock.GetUtcNow().UtcDateTime); var user = (await users.GetUserAsync(User))!; return View(await reports.GetAsync(from ?? now.Date.AddDays(-30), to ?? now.Date, user.CinemaClusterId, User.IsInRole(Roles.Admin))); }
}
