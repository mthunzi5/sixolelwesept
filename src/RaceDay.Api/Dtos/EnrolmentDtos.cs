using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>A participant's entry into a category of an event.</summary>
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

    /// <summary>Entry fee of the chosen category.</summary>
    public decimal EntryFee { get; set; }

    public string BibNumber { get; set; } = string.Empty;
    public DateTime EnrolmentDate { get; set; }
    public EnrolmentStatus Status { get; set; }

    /// <summary>Status of the entry-fee payment, or null if nothing has been paid.</summary>
    public PaymentStatus? PaymentStatus { get; set; }

    /// <summary>ID of the captured result, or null if no result has been captured yet.</summary>
    public int? ResultId { get; set; }

    public int? FinishTimeSeconds { get; set; }
    public int? Position { get; set; }
    public ResultStatus? ResultStatus { get; set; }
}
