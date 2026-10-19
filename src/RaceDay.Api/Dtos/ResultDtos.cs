using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>A captured race result.</summary>
public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string BibNumber { get; set; } = string.Empty;
    public int? FinishTimeSeconds { get; set; }

    /// <summary>Finish time formatted as hh:mm:ss, for display.</summary>
    public string? FinishTime { get; set; }

    public int? Position { get; set; }
    public ResultStatus Status { get; set; }
}

/// <summary>Finish time and position for a participant. Finished results need both; DNF or DSQ results must leave them empty.</summary>
public class ResultRequest
{
    /// <summary>Finish time in seconds (e.g. 3125 = 52:05).</summary>
    /// <example>3125</example>
    [Range(1, 864000)]
    public int? FinishTimeSeconds { get; set; }

    /// <summary>Finishing position within the category, starting at 1.</summary>
    /// <example>1</example>
    [Range(1, 100000)]
    public int? Position { get; set; }

    /// <summary>Finished, DNF (did not finish) or DSQ (disqualified). Defaults to Finished.</summary>
    /// <example>Finished</example>
    public ResultStatus Status { get; set; } = ResultStatus.Finished;
}
