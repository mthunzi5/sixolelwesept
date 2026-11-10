using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Auth;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>View and update your own profile, profile picture and password. Available to both roles.</summary>
[RequireRole]
public class ProfileController : RaceDayControllerBase
{
    private readonly RaceDayApiClient _api;

    public ProfileController(RaceDayApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index()
    {
        var me = await _api.GetMeAsync();
        if (RedirectForAuthFailure(me) is IActionResult redirect) return redirect;
        if (!me.Success || me.Data is null)
        {
            FlashError(me.Error);
            return RedirectToAction("Index", "Home");
        }

        return View(new ProfileViewModel
        {
            User = me.Data,
            Profile = new ProfileForm { FullName = me.Data.FullName, PhoneNumber = me.Data.PhoneNumber }
        });
    }

    [HttpPost]
    public async Task<IActionResult> Update([Bind(Prefix = "Profile")] ProfileForm form)
    {
        if (!ModelState.IsValid) return await Redisplay(profile: form);

        var result = await _api.UpdateMeAsync(form);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;
        if (!result.Success || result.Data is null)
        {
            AddApiError(result);
            return await Redisplay(profile: form);
        }

        HttpContext.Session.SignIn(result.Data);
        FlashSuccess("Your profile has been updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            FlashError("Choose an image to upload.");
            return RedirectToAction(nameof(Index));
        }

        // Sent to the API, which stores it in Azure Blob Storage.
        var result = await _api.UploadProfilePictureAsync(picture);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;

        if (result.Success)
        {
            HttpContext.Session.SetString(SessionKeys.ProfileImageUrl, result.Data?.ImageUrl ?? string.Empty);
            FlashSuccess("Looking good! Your profile picture has been updated.");
        }
        else
        {
            FlashError(result.Error);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordForm form)
    {
        if (!ModelState.IsValid) return await Redisplay(password: form);

        var result = await _api.ChangePasswordAsync(form);
        if (result.IsUnauthorized && HttpContext.Session.IsSignedIn())
        {
            // The API answers 401 both for "not logged in" and "current password wrong".
            // Our session is still valid, so treat it as a wrong current password.
            ModelState.AddModelError("Password.CurrentPassword", "Your current password is incorrect.");
            return await Redisplay(password: new ChangePasswordForm());
        }

        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;
        if (!result.Success)
        {
            AddApiError(result);
            return await Redisplay(password: new ChangePasswordForm());
        }

        FlashSuccess("Your password has been changed.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Re-shows the profile page with the submitted form and its errors.</summary>
    private async Task<IActionResult> Redisplay(ProfileForm? profile = null, ChangePasswordForm? password = null)
    {
        var me = await _api.GetMeAsync();
        if (RedirectForAuthFailure(me) is IActionResult redirect) return redirect;

        var user = me.Data ?? new UserDto { FullName = HttpContext.Session.GetFullName() };
        return View(nameof(Index), new ProfileViewModel
        {
            User = user,
            Profile = profile ?? new ProfileForm { FullName = user.FullName, PhoneNumber = user.PhoneNumber },
            Password = password ?? new ChangePasswordForm()
        });
    }
}
