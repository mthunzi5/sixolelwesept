using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>Entity to DTO conversions. Navigation properties must be loaded with Include before calling these.</summary>
public static class Mappings
{
    public static UserDto ToDto(this User u) => new()
    {
        UserId = u.UserId,
        FullName = u.FullName,
        Email = u.Email,
        Role = u.Role,
        PhoneNumber = u.PhoneNumber,
        CreatedAt = u.CreatedAt
    };

    public static PublicUserDto ToPublicDto(this User u) => new()
    {
        UserId = u.UserId,
        FullName = u.FullName,
        Role = u.Role
    };

    public static EventDto ToDto(this Event e) => Fill(new EventDto(), e);

    public static EventDetailDto ToDetailDto(this Event e)
    {
        var dto = Fill(new EventDetailDto(), e);
        dto.Categories = e.Categories.OrderBy(c => c.DistanceKm).ThenBy(c => c.CategoryName).Select(c => c.ToDto()).ToList();
        return dto;
    }

    private static T Fill<T>(T dto, Event e) where T : EventDto
    {
        dto.EventId = e.EventId;
        dto.OrganiserId = e.OrganiserId;
        dto.OrganiserName = e.Organiser?.FullName ?? string.Empty;
        dto.EventName = e.EventName;
        dto.Description = e.Description;
        dto.EventDate = e.EventDate;
        dto.Location = e.Location;
        dto.DistanceKm = e.DistanceKm;
        dto.EventType = e.EventType;
        dto.Status = e.Status;
        return dto;
    }

    public static CategoryDto ToDto(this Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        CategoryName = c.CategoryName,
        CategoryType = c.CategoryType,
        DistanceKm = c.DistanceKm,
        MinAge = c.MinAge,
        MaxAge = c.MaxAge,
        EntryFee = c.EntryFee,
        MaxParticipants = c.MaxParticipants,
        SpotsTaken = c.Enrolments.Count(en => en.Status != EnrolmentStatus.Withdrawn)
    };

    public static EnrolmentDto ToDto(this Enrolment en) => new()
    {
        EnrolmentId = en.EnrolmentId,
        ParticipantId = en.ParticipantId,
        ParticipantName = en.Participant?.FullName ?? string.Empty,
        EventId = en.Category?.EventId ?? 0,
        EventName = en.Category?.Event?.EventName ?? string.Empty,
        CategoryId = en.CategoryId,
        CategoryName = en.Category?.CategoryName ?? string.Empty,
        BibNumber = en.BibNumber,
        EnrolmentDate = en.EnrolmentDate,
        Status = en.Status
    };

    public static ResultDto ToDto(this Result r) => new()
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        ParticipantId = r.Enrolment?.ParticipantId ?? 0,
        ParticipantName = r.Enrolment?.Participant?.FullName ?? string.Empty,
        EventId = r.Enrolment?.Category?.EventId ?? 0,
        EventName = r.Enrolment?.Category?.Event?.EventName ?? string.Empty,
        CategoryId = r.Enrolment?.CategoryId ?? 0,
        CategoryName = r.Enrolment?.Category?.CategoryName ?? string.Empty,
        BibNumber = r.Enrolment?.BibNumber ?? string.Empty,
        FinishTimeSeconds = r.FinishTimeSeconds,
        FinishTime = r.FinishTimeSeconds is int s ? TimeSpan.FromSeconds(s).ToString(@"hh\:mm\:ss") : null,
        Position = r.Position,
        Status = r.Status
    };

    public static PaymentDto ToDto(this Payment p) => new()
    {
        PaymentId = p.PaymentId,
        EnrolmentId = p.EnrolmentId,
        Amount = p.Amount,
        PaymentMethod = p.PaymentMethod,
        PaymentDate = p.PaymentDate,
        Status = p.Status
    };
}
