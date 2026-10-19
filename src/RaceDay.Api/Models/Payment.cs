namespace RaceDay.Api.Models;

/// <summary>Entry-fee payment for one enrolment (at most one per enrolment).</summary>
public class Payment
{
    public int PaymentId { get; set; }
    public int EnrolmentId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public Enrolment? Enrolment { get; set; }
}
