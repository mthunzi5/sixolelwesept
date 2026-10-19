namespace RaceDay.Api.Models;

/// <summary>The finishing result captured by an Organiser for one enrolment (at most one per enrolment).</summary>
public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int? FinishTimeSeconds { get; set; }
    public int? Position { get; set; }
    public ResultStatus Status { get; set; } = ResultStatus.Finished;

    public Enrolment? Enrolment { get; set; }
}
