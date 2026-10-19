using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>An event category as returned by the API.</summary>
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

    /// <summary>Number of active (non-withdrawn) enrolments in this category.</summary>
    public int SpotsTaken { get; set; }
}

/// <summary>Data needed to create or update a category, either a distance category (e.g. 10km) or an age category (e.g. Under 20).</summary>
public class CategoryRequest
{
    /// <example>10km Road Race</example>
    [Required, StringLength(50, MinimumLength = 2)]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Distance or Age. Defaults to Distance.</summary>
    /// <example>Distance</example>
    public CategoryType CategoryType { get; set; } = CategoryType.Distance;

    /// <summary>Distance raced in this category, in kilometres.</summary>
    /// <example>10</example>
    [Range(0.1, 999.99)]
    public decimal DistanceKm { get; set; }

    /// <summary>Age categories only: youngest age allowed (inclusive).</summary>
    [Range(0, 120)]
    public int? MinAge { get; set; }

    /// <summary>Age categories only: oldest age allowed (inclusive).</summary>
    [Range(0, 120)]
    public int? MaxAge { get; set; }

    /// <example>150</example>
    [Range(0, 999999.99)]
    public decimal EntryFee { get; set; }

    /// <example>300</example>
    [Range(1, 100000)]
    public int MaxParticipants { get; set; }
}
