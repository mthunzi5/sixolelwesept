using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Mvc.Models;

namespace RaceDay.Mvc.Auth;

/// <summary>
/// Protects MVC pages using the website session.
/// <list type="bullet">
/// <item>Not signed in: redirect to the login page, then back here after logging in.</item>
/// <item>Signed in with the wrong role: redirect to the Access Denied page.</item>
/// </list>
/// With no roles given, any signed-in user may open the page.
/// The API runs its own checks as well, so this filter decides navigation, not security on its own.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    public RequireRoleAttribute(params UserRole[] roles)
    {
        Roles = roles;
    }

    public UserRole[] Roles { get; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;
        var session = httpContext.Session;

        if (!session.IsSignedIn())
        {
            var tempData = httpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(httpContext);
            tempData["Info"] = "Please log in to continue.";

            // Only GET pages are worth returning to; a form post would be lost anyway.
            var returnUrl = HttpMethods.IsGet(httpContext.Request.Method)
                ? httpContext.Request.Path + httpContext.Request.QueryString
                : null;
            context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl });
            return;
        }

        if (Roles.Length > 0 && !Roles.Contains(session.GetUserRole()!.Value))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
        }
    }
}
