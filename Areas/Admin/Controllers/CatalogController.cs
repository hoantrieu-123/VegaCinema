using System.Security.Claims;
using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace VegaCinema.Areas.Admin.Controllers;
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class CatalogController(AdminService admin) : Controller
{
    public async Task<IActionResult> Index(string section = "movies", string? search = null, string? filter = null, int page = 1)
    { ViewBag.Search = search; ViewBag.Filter = filter; return View(await admin.ListAsync(section, search, filter, page)); }
    [HttpGet] public async Task<IActionResult> Edit(string section, string? id) => View(await admin.EditorAsync(section, id));
    [HttpPost] public async Task<IActionResult> Edit(string section, string? id, Dictionary<string, string> values)
    {
        try { await admin.SaveAsync(section, id, values, User.FindFirstValue(ClaimTypes.NameIdentifier)!); TempData["Success"] = "Đã lưu thay đổi."; return RedirectToAction(nameof(Index), new { section }); }
        catch (BusinessException e)
        { ModelState.AddModelError("", e.Message); var editor = await admin.EditorAsync(section, id); return View(editor with { Fields = editor.Fields.Select(f => f with { Value = values.GetValueOrDefault(f.Name, f.Value) }).ToList() }); }
    }
}
