using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>Details needed to create a new account.</summary>
public class RegisterRequest
{
    /// <example>Thandi Mbeki</example>
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <example>thandi@example.com</example>
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>At least 8 characters. Stored only as a BCrypt hash.</summary>
    /// <example>Password123!</example>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>The account type: Organiser or Participant. It cannot be changed after registration.</summary>
    /// <example>Participant</example>
    [Required]
    public UserRole? Role { get; set; }

    /// <example>0821234567</example>
    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}

/// <summary>Credentials used to log in.</summary>
public class LoginRequest
{
    /// <example>amahle.nkosi@example.com</example>
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <example>Password123!</example>
    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Returned after a successful login. The session cookie is set on the same response.</summary>
public class LoginResponse
{
    public string Message { get; set; } = string.Empty;
    public UserDto User { get; set; } = new();
}

/// <summary>Who is currently logged in on this session.</summary>
public class SessionInfo
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
