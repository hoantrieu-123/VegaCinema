using System.Security.Claims;
using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace VegaCinema.Controllers;
[Authorize]
public class TicketsController(TicketService tickets, IQrCode qr, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Qr(string code)
    {
        var user = (await users.GetUserAsync(User))!;
        var ticket = await tickets.Query().SingleOrDefaultAsync(x => x.Code == code && (x.BookingDetail.Booking.UserId == user.Id || User.IsInRole(Roles.Admin) ||
            (User.IsInRole(Roles.Staff) && x.BookingDetail.Booking.Showtime.ScreeningRoom.Cinema.CinemaClusterId == user.CinemaClusterId)));
        if (ticket is null) return NotFound(); Response.Headers.CacheControl = "no-store"; return File(qr.CreatePng(ticket.Code), "image/png");
    }
}
