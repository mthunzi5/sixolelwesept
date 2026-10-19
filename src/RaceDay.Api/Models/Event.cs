namespace RaceDay.Api.Models;

/// <summary>A race day created and owned by an Organiser.</summary>
public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Headline distance of the event in kilometres (added in Part 2 - see README deviations).</summary>
    public decimal DistanceKm { get; set; }

    /// <summary>Run, Walk or Cycle (added in Part 2 - see README deviations).</summary>
    public EventType EventType { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Scheduled;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? Organiser { get; set; }
    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
