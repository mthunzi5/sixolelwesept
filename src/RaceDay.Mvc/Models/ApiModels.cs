namespace RaceDay.Mvc.Models;

// Shapes of the JSON the RaceDay API sends back. These are plain data holders: every rule
// (who may do what, capacity, ownership, statuses) is enforced by the API, not here.

public enum UserRole { Organiser, Participant }

public enum EventType { Run, Walk, Cycle }

public enum EventStatus { Scheduled, Ongoing, Completed, Cancelled }

public enum CategoryType { Distance, Age }

public enum EnrolmentStatus { Pending, Confirmed, Withdrawn }

public enum ResultStatus { Finished, DNF, DSQ }

public enum PaymentMethod { Card, EFT, Cash }

public enum PaymentStatus { Pending, Paid, Refunded }

public class UserDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LoginResponse
{
    public string Message { get; set; } = string.Empty;
    public UserDto User { get; set; } = new();
}

public class EventDto
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public EventType EventType { get; set; }
    public EventStatus Status { get; set; }
    public string? BannerImageUrl { get; set; }
    public int EnrolmentCount { get; set; }

    /// <summary>Only filled in by GET /api/events/{id}.</summary>
    public List<CategoryDto> Categories { get; set; } = new();
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public CategoryType CategoryType { get; set; }
    public decimal DistanceKm { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }
    public int SpotsTaken { get; set; }
}

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly? EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal EntryFee { get; set; }
    public string BibNumber { get; set; } = string.Empty;
    public DateTime EnrolmentDate { get; set; }
    public EnrolmentStatus Status { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public int? ResultId { get; set; }
    public int? FinishTimeSeconds { get; set; }
    public int? Position { get; set; }
    public ResultStatus? ResultStatus { get; set; }
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly? EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string BibNumber { get; set; } = string.Empty;
    public int? FinishTimeSeconds { get; set; }
    public string? FinishTime { get; set; }
    public int? Position { get; set; }
    public ResultStatus Status { get; set; }
    public int FinishersInCategory { get; set; }
}

public class PaymentDto
{
    public int PaymentId { get; set; }
    public int EnrolmentId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentStatus Status { get; set; }
}

public class ImageUploadResponse
{
    public string? ImageUrl { get; set; }
}
