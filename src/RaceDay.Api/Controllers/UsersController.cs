using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Controllers;

/// <summary>Profile management. Both roles can view and update their own profile.</summary>
[Route("api/users")]
[SessionAuthorize]
public class UsersController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Get the logged-in user's own profile.</summary>
    /// <response code="200">The user's profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe()
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        return user is null ? NotFoundProblem("Your account no longer exists.") : Ok(user.ToDto());
    }

    /// <summary>Update the logged-in user's name and phone number.</summary>
    /// <remarks>Email and role cannot be changed.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null)
        {
            return NotFoundProblem("Your account no longer exists.");
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        await _db.SaveChangesAsync();

        HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
        return Ok(user.ToDto());
    }

    /// <summary>Change the logged-in user's password.</summary>
    /// <response code="200">Password changed.</response>
    /// <response code="400">The new password is too short or the same as the current one.</response>
    /// <response code="401">Not logged in, or the current password is wrong.</response>
    [HttpPut("me/password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null)
        {
            return NotFoundProblem("Your account no longer exists.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return UnauthorizedProblem("The current password is incorrect.");
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            return BadRequestProblem("The new password must be different from the current password.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Password changed." });
    }

    /// <summary>Get another user's public profile (name and role only).</summary>
    /// <response code="200">The public profile.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">No user with that ID.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PublicUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _db.Users.FindAsync(id);
        return user is null ? NotFoundProblem($"User {id} was not found.") : Ok(user.ToPublicDto());
    }
}
