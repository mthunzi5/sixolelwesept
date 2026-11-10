using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Auth;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>Register, log in and log out. All credential checks happen in the API.</summary>
public class AuthController : RaceDayControllerBase
{
    private readonly RaceDayApiClient _api;

    public AuthController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (HttpContext.Session.IsSignedIn()) return RedirectToHome();
        return View(new LoginForm { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginForm form)
    {
        if (!ModelState.IsValid) return View(form);

        var result = await _api.LoginAsync(form.Email, form.Password);
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.StatusCode == StatusCodes.Status401Unauthorized
                ? "That email and password don't match an account."
                : result.Error ?? "We couldn't log you in. Please try again.");
            form.Password = string.Empty;
            return View(form);
        }

        HttpContext.Session.SignIn(result.Data.User);
        FlashSuccess($"Welcome back, {result.Data.User.FullName.Split(' ')[0]}!");

        return Url.IsLocalUrl(form.ReturnUrl) ? Redirect(form.ReturnUrl!) : RedirectToHome();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (HttpContext.Session.IsSignedIn()) return RedirectToHome();
        return View(new RegisterForm());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterForm form)
    {
        if (!ModelState.IsValid) return View(form);

        var registered = await _api.RegisterAsync(form);
        if (!registered.Success)
        {
            ModelState.AddModelError(registered.StatusCode == StatusCodes.Status409Conflict ? nameof(form.Email) : string.Empty,
                registered.Error ?? "We couldn't create your account. Please try again.");
            return View(form);
        }

        // Sign straight in with the new account so the user lands on their own pages.
        var login = await _api.LoginAsync(form.Email, form.Password);
        if (!login.Success || login.Data is null)
        {
            FlashSuccess("Your account is ready. Please log in.");
            return RedirectToAction(nameof(Login));
        }

        HttpContext.Session.SignIn(login.Data.User);
        FlashSuccess($"Welcome to RaceDay, {login.Data.User.FullName.Split(' ')[0]}! Your {login.Data.User.Role} account is ready.");
        return RedirectToHome();
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        if (HttpContext.Session.IsSignedIn())
        {
            await _api.LogoutAsync();
        }

        HttpContext.Session.Clear();
        FlashInfo("You've been logged out. See you at the start line!");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    /// <summary>Each role has its own landing page.</summary>
    private IActionResult RedirectToHome() => HttpContext.Session.IsOrganiser()
        ? RedirectToAction("Dashboard", "Organiser")
        : RedirectToAction("Index", "Home");
}
