using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VegaCinema.ViewModels;
namespace VegaCinema.Controllers;
public class MoviesController(MovieService movies, CinemaService cinemas, ShowtimeService shows) : Controller
{
    public async Task<IActionResult> Index(string? search, string? genre, MovieStatus? status, int page = 1)
    {
        page = Math.Max(1, page); var query = movies.Query(search, genre, status);
        return View(new MoviePage(await query.Skip((page - 1) * 12).Take(12).ToListAsync(), search, genre, status, page, await query.CountAsync()));
    }
    public async Task<IActionResult> Details(int id, DateTime? date, int? cinemaId)
    {
        var movie = await movies.FindAsync(id); if (movie is null) return NotFound();
        return View(new MovieDetails(movie, await shows.Query(id, cinemaId, date).Take(100).ToListAsync(), await cinemas.ListAsync(), date, cinemaId));
    }
}
