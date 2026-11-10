using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>An event as returned by the API.</summary>
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
    /// <summary>API path of the banner image stored in Azure Blob Storage, or null if none has been uploaded.</summary>
    public string? BannerImageUrl { get; set; }

    /// <summary>Number of active (non-withdrawn) enrolments across all of the event's categories.</summary>
    public int EnrolmentCount { get; set; }
}

/// <summary>An event together with its categories.</summary>
public class EventDetailDto : EventDto
{
    public List<CategoryDto> Categories { get; set; } = new();
}

/// <summary>Data needed to create or update an event.</summary>
public class EventRequest
{
    /// <example>Soweto 10km Challenge</example>
    [Required, StringLength(150, MinimumLength = 3)]
    public string EventName { get; set; } = string.Empty;

    /// <example>A flat, fast 10km road race through Soweto.</example>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>Date of the race (yyyy-MM-dd).</summary>
    /// <example>2026-12-13</example>
    [Required]
    public DateOnly? EventDate { get; set; }

    /// <example>Soweto, Gauteng</example>
    [Required, StringLength(150, MinimumLength = 2)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Headline distance of the event in kilometres.</summary>
    /// <example>10</example>
    [Range(0.1, 999.99)]
    public decimal DistanceKm { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    /// <example>Run</example>
    [Required]
    public EventType? EventType { get; set; }

    /// <summary>Optional. Defaults to Scheduled when creating, and stays unchanged when updating if left out.</summary>
    public EventStatus? Status { get; set; }
}
