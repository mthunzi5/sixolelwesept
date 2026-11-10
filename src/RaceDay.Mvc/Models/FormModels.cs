using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.Models;

// Form models. The data annotations give instant, friendly feedback on the page; the API
// still validates everything again and has the final say.

public class LoginForm
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterForm
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Your name must be 2 to 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Choose whether you are an Organiser or a Participant.")]
    [Display(Name = "I am a")]
    public UserRole? Role { get; set; }

    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileForm
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Your name must be 2 to 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }
}

public class ChangePasswordForm
{
    [Required(ErrorMessage = "Enter your current password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a new password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Your new password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "The passwords don't match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class EventForm
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Give the event a name.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "The name must be 3 to 150 characters.")]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Keep the description under 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose the event date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Event date")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Enter where the event takes place.")]
    [StringLength(150, MinimumLength = 2)]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the distance.")]
    [Range(0.1, 999.99, ErrorMessage = "Distance must be between 0.1 and 999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Choose the event type.")]
    [Display(Name = "Event type")]
    public EventType? EventType { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Scheduled;

    [Display(Name = "Banner image")]
    public IFormFile? BannerImage { get; set; }

    /// <summary>The banner already stored for this event (edit only), shown as a preview.</summary>
    public string? CurrentBannerUrl { get; set; }
}

public class CategoryForm
{
    [Required(ErrorMessage = "Give the category a name, e.g. 10km or Under 20.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "The name must be 2 to 50 characters.")]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Display(Name = "Category type")]
    public CategoryType CategoryType { get; set; } = CategoryType.Distance;

    [Required(ErrorMessage = "Enter the distance raced.")]
    [Range(0.1, 999.99, ErrorMessage = "Distance must be between 0.1 and 999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Range(0, 120, ErrorMessage = "Enter an age between 0 and 120.")]
    [Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Range(0, 120, ErrorMessage = "Enter an age between 0 and 120.")]
    [Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }

    [Required(ErrorMessage = "Enter the entry fee (0 for free).")]
    [Range(0, 999999.99, ErrorMessage = "Enter a fee of 0 or more.")]
    [Display(Name = "Entry fee (R)")]
    public decimal? EntryFee { get; set; }

    [Required(ErrorMessage = "Enter the maximum number of participants.")]
    [Range(1, 100000, ErrorMessage = "Allow at least 1 participant.")]
    [Display(Name = "Max participants")]
    public int? MaxParticipants { get; set; }
}

public class ResultForm
{
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }

    /// <summary>Finish time typed as h:mm:ss or mm:ss.</summary>
    [RegularExpression(@"^\s*(\d{1,3}:)?[0-5]?\d:[0-5]\d\s*$", ErrorMessage = "Use h:mm:ss, e.g. 1:05:30.")]
    [Display(Name = "Finish time")]
    public string? FinishTime { get; set; }

    [Range(1, 100000, ErrorMessage = "Position must be 1 or more.")]
    public int? Position { get; set; }

    public ResultStatus Status { get; set; } = ResultStatus.Finished;
}

public class PaymentForm
{
    public int EnrolmentId { get; set; }
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Choose a payment method.")]
    public PaymentMethod? PaymentMethod { get; set; }
}

public class EventFilter
{
    [Display(Name = "Search")]
    public string? Search { get; set; }

    [Display(Name = "Type")]
    public EventType? EventType { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "From")]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "To")]
    public DateTime? To { get; set; }
}
