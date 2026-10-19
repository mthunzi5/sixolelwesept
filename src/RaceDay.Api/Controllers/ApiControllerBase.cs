using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Shared helpers for reading the logged-in user out of the session and building error responses.</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>ID of the logged-in user. Only meaningful on endpoints marked with [SessionAuthorize].</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    /// <summary>Role of the logged-in user. Only meaningful on endpoints marked with [SessionAuthorize].</summary>
    protected UserRole CurrentRole => Enum.Parse<UserRole>(HttpContext.Session.GetString(SessionKeys.Role)!);

    /// <summary>403 response used when a user has the right role but does not own the resource.</summary>
    protected ObjectResult ForbiddenProblem(string detail) => ProblemResult(StatusCodes.Status403Forbidden, "Forbidden", detail);

    protected ObjectResult NotFoundProblem(string detail) => ProblemResult(StatusCodes.Status404NotFound, "Not Found", detail);

    protected ObjectResult ConflictProblem(string detail) => ProblemResult(StatusCodes.Status409Conflict, "Conflict", detail);

    protected ObjectResult BadRequestProblem(string detail) => ProblemResult(StatusCodes.Status400BadRequest, "Bad Request", detail);

    protected ObjectResult UnauthorizedProblem(string detail) => ProblemResult(StatusCodes.Status401Unauthorized, "Unauthorized", detail);

    private static ObjectResult ProblemResult(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
