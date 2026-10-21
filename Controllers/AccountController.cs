using CinemaBooking.Business.Services;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VegaCinema.ViewModels;
namespace VegaCinema.Controllers;
public class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, UserService service) : Controller
{
    [HttpGet] public IActionResult Login(string? returnUrl) => View(new LoginInput { ReturnUrl = returnUrl });
    [HttpPost] public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var user = await users.FindByEmailAsync(input.Email);
        var result = user is { IsActive: true } ? await signIn.PasswordSignInAsync(user, input.Password, input.RememberMe, lockoutOnFailure: true) : Microsoft.AspNetCore.Identity.SignInResult.Failed;
        if (result.Succeeded) return LocalRedirect(Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : "/");
        ModelState.AddModelError("", result.IsLockedOut ? "Tài khoản bị khóa tạm thời sau 5 lần nhập sai. Vui lòng thử lại sau 15 phút." : "Email hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.");
        return View(input);
    }
    [HttpGet] public IActionResult Register() => View(new RegisterInput());
    [HttpPost] public async Task<IActionResult> Register(RegisterInput input)
    {
        if (!ModelState.IsValid) return View(input);
        try { var user = await service.RegisterAsync(input.FullName, input.Email, input.Password); await signIn.SignInAsync(user, false); return RedirectToAction("Index", "Home"); }
        catch (BusinessException e) { ModelState.AddModelError("", e.Message); return View(input); }
    }
    [Authorize, HttpPost] public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return RedirectToAction("Index", "Home"); }
    [Authorize, HttpGet] public async Task<IActionResult> Profile()
    { var u = (await users.GetUserAsync(User))!; return View(new ProfileInput { FullName = u.FullName, Email = u.Email!, Phone = u.PhoneNumber }); }
    [Authorize, HttpPost] public async Task<IActionResult> Profile(ProfileInput input)
    {
        var user = (await users.GetUserAsync(User))!; input.Email = user.Email!;
        if (!ModelState.IsValid) return View(input);
        await service.UpdateProfileAsync(user, input.FullName, input.Phone); TempData["Success"] = "Đã cập nhật thông tin cá nhân."; return RedirectToAction(nameof(Profile));
    }
    [Authorize, HttpGet] public IActionResult Password() => View(new PasswordInput());
    [Authorize, HttpPost] public async Task<IActionResult> Password(PasswordInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var user = (await users.GetUserAsync(User))!;
        try { UserService.Check(await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword)); await signIn.RefreshSignInAsync(user); TempData["Success"] = "Đã đổi mật khẩu."; return RedirectToAction(nameof(Profile)); }
        catch (BusinessException e) { ModelState.AddModelError("", e.Message); return View(input); }
    }
    public IActionResult Denied() { Response.StatusCode = 403; return View(); }
}
