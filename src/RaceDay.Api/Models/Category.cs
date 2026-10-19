namespace RaceDay.Api.Models;

/// <summary>An entry category within an event, by distance (e.g. 10km) or by age group (e.g. Under 20).</summary>
public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Distance or Age (added in Part 2 - see README deviations).</summary>
    public CategoryType CategoryType { get; set; } = CategoryType.Distance;

    public decimal DistanceKm { get; set; }

    /// <summary>Lower age bound for age categories (inclusive), null when not applicable.</summary>
    public int? MinAge { get; set; }

    /// <summary>Upper age bound for age categories (inclusive), null when not applicable.</summary>
    public int? MaxAge { get; set; }

    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }

    public Event? Event { get; set; }
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
