namespace RaceDay.Api.Models;

/// <summary>Links a Participant to the Category (and therefore Event) they entered.</summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public string BibNumber { get; set; } = string.Empty;
    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Confirmed;

    public User? Participant { get; set; }
    public Category? Category { get; set; }
    public Result? Result { get; set; }
    public Payment? Payment { get; set; }
}
