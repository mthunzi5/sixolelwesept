using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Auth;

/// <summary>
/// Protects an endpoint using the server-side session created at login.
/// <list type="bullet">
/// <item>No logged-in session: <b>401 Unauthorized</b>.</item>
/// <item>Logged in, but the session role is not one of <see cref="Roles"/>: <b>403 Forbidden</b>.</item>
/// </list>
/// With no roles given, any logged-in user (either role) is allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public SessionAuthorizeAttribute(params UserRole[] roles)
    {
        Roles = roles;
    }

    /// <summary>Roles allowed to call the endpoint. Empty means any logged-in user.</summary>
    public UserRole[] Roles { get; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        var roleName = session.GetString(SessionKeys.Role);

        if (userId is null || !Enum.TryParse<UserRole>(roleName, out var role))
        {
            context.Result = Problem(StatusCodes.Status401Unauthorized, "Unauthorized",
                "You must log in via POST /api/auth/login to access this endpoint.");
            return;
        }

        if (Roles.Length > 0 && !Roles.Contains(role))
        {
            context.Result = Problem(StatusCodes.Status403Forbidden, "Forbidden",
                $"This endpoint is only available to the {string.Join(" / ", Roles)} role. You are logged in as {(role == UserRole.Organiser ? "an" : "a")} {role}.");
        }
    }

    private static ObjectResult Problem(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
