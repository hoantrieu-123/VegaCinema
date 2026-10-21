using CinemaBooking.Business.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VegaCinema.ViewModels;
using CinemaBooking.Domain;
namespace VegaCinema.Controllers;
public class ScheduleController(ShowtimeService shows, MovieService movies, CinemaService cinemas, TimeProvider clock) : Controller
{
    public async Task<IActionResult> Index(int? movieId, int? cinemaId, DateTime? date)
    {
        var selected = date?.Date ?? VietnamTime.Local(clock.GetUtcNow().UtcDateTime).Date;
        return View(new SchedulePage(await shows.Query(movieId, cinemaId, selected).Take(300).ToListAsync(), await movies.Query(status: MovieStatus.NOW_SHOWING).ToListAsync(), await cinemas.ListAsync(), movieId, cinemaId, selected));
    }
}
public class CinemasController(CinemaService cinemas) : Controller
{ public async Task<IActionResult> Index() => View(await cinemas.ListAsync()); }
