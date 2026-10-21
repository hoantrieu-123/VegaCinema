using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace VegaCinema.Areas.Admin.Controllers;
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class DashboardController(ReportService reports, TimeProvider clock) : Controller
{
    public async Task<IActionResult> Index()
    { var now = VietnamTime.Local(clock.GetUtcNow().UtcDateTime); return View(await reports.GetAsync(now.Date.AddDays(-30), now.Date, null, true)); }
}
