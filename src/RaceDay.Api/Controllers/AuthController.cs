using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout. Login starts a server-side session that every protected endpoint relies on.</summary>
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public AuthController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Register a new account as either an Organiser or a Participant.</summary>
    /// <remarks>
    /// The role is chosen once at registration and cannot be changed later. The password is
    /// hashed with BCrypt and only the hash is stored. Registering does not log you in, so
    /// call POST /api/auth/login afterwards.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user's profile (without the password hash).</response>
    /// <response code="400">Validation failed, for example a missing field, invalid email, short password or unknown role.</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (request.Role is not UserRole role || !Enum.IsDefined(role))
        {
            return BadRequestProblem("Role must be either 'Organiser' or 'Participant'.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return ConflictProblem($"An account with the email '{email}' already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim()
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created($"/api/users/{user.UserId}", user.ToDto());
    }

    /// <summary>Log in with email and password and start a session.</summary>
    /// <remarks>
    /// On success the server stores your user ID and role in the session and sends back a
    /// <c>.RaceDay.Session</c> cookie. Your browser (and Swagger UI) sends that cookie
    /// automatically on every later request, which is how protected endpoints know who you are.
    ///
    /// Seeded demo accounts all use the password <c>Password123!</c>, for example
    /// <c>naledi.khumalo@raceday.co.za</c> (Organiser) or <c>amahle.nkosi@example.com</c> (Participant).
    /// </remarks>
    /// <response code="200">Logged in. Returns the user's profile and sets the session cookie.</response>
    /// <response code="400">Email or password missing.</response>
    /// <response code="401">The email or password is incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);

        // Same message for "no such user" and "wrong password" so emails can't be probed.
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return UnauthorizedProblem("Invalid email or password.");
        }

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.ToString());
        HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);

        return Ok(new LoginResponse { Message = $"Logged in as {user.Role}.", User = user.ToDto() });
    }

    /// <summary>Show who is logged in on the current session.</summary>
    /// <response code="200">The logged-in user's ID, name and role.</response>
    /// <response code="401">No one is logged in.</response>
    [HttpGet("session")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(SessionInfo), StatusCodes.Status200OK)]
    public IActionResult CurrentSession() => Ok(new SessionInfo
    {
        UserId = CurrentUserId,
        Role = CurrentRole,
        FullName = HttpContext.Session.GetString(SessionKeys.FullName) ?? string.Empty
    });

    /// <summary>Log out and end the current session.</summary>
    /// <response code="204">Session cleared. Protected endpoints return 401 until you log in again.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        Response.Cookies.Delete(".RaceDay.Session");
        return NoContent();
    }
}
