using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Auth;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>Shared helpers: the signed-in user, flash messages, and reacting to API auth failures.</summary>
public abstract class RaceDayControllerBase : Controller
{
    protected int CurrentUserId => HttpContext.Session.GetUserId() ?? 0;

    protected void FlashSuccess(string message) => TempData["Success"] = message;

    protected void FlashError(string? message) => TempData["Error"] = message ?? "Something went wrong. Please try again.";

    protected void FlashInfo(string message) => TempData["Info"] = message;

    /// <summary>
    /// If the API rejected the call because the session expired (401) or the role is wrong (403),
    /// returns the redirect to send the user to. Otherwise returns null and the caller shows the error itself.
    /// </summary>
    protected IActionResult? RedirectForAuthFailure(ApiResult result)
    {
        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            FlashInfo("Your session has expired. Please log in again.");
            var returnUrl = HttpMethods.IsGet(Request.Method) ? Request.Path + Request.QueryString : null;
            return RedirectToAction("Login", "Auth", new { returnUrl });
        }

        if (result.StatusCode == StatusCodes.Status403Forbidden)
        {
            FlashError(result.Error);
            return RedirectToAction("AccessDenied", "Auth");
        }

        return null;
    }

    /// <summary>Copies an API error onto the form so it shows in the validation summary.</summary>
    protected void AddApiError(ApiResult result) => ModelState.AddModelError(string.Empty, result.Error ?? "Something went wrong. Please try again.");
}
