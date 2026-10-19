using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>A user's own profile. The password hash is never included.</summary>
public class UserDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>The fields of a user that other users are allowed to see.</summary>
public class PublicUserDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

/// <summary>Profile fields a user may change. Email and role are fixed.</summary>
public class UpdateProfileRequest
{
    /// <example>Amahle Nkosi-Zulu</example>
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <example>0731234699</example>
    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}

/// <summary>Changes the logged-in user's password.</summary>
public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>At least 8 characters.</summary>
    [Required, StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
