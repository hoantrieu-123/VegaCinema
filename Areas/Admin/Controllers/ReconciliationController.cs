using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace VegaCinema.Areas.Admin.Controllers;
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class ReconciliationController(PaymentService payments) : Controller
{
    [HttpPost] public async Task<IActionResult> Retry(Guid paymentId, Guid bookingId)
    { await payments.RetryAsync(paymentId); TempData["Success"] = "Đã xử lý lại xác nhận thanh toán đã lưu."; return RedirectToAction("Details", "Booking", new { area = "", id = bookingId }); }
}
