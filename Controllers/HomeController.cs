using System.Diagnostics;
using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.EntityFrameworkCore;
using VegaCinema.ViewModels;
using Microsoft.AspNetCore.Mvc;
using VegaCinema.Models;

namespace VegaCinema.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index([FromServices] MovieService movies, [FromServices] CinemaService cinemas, [FromServices] ShowtimeService shows)
            => View(new HomePage(await movies.Query(status: MovieStatus.NOW_SHOWING).ToListAsync(),
                await movies.Query(status: MovieStatus.COMING_SOON).ToListAsync(), await cinemas.ListAsync(), await shows.Query().Take(6).ToListAsync()));

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
