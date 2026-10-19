using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>An entry-fee payment.</summary>
public class PaymentDto
{
    public int PaymentId { get; set; }
    public int EnrolmentId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentStatus Status { get; set; }
}

/// <summary>Records payment of a category's entry fee.</summary>
public class PaymentRequest
{
    /// <summary>Must equal the category's entry fee.</summary>
    /// <example>180</example>
    [Range(0, 999999.99)]
    public decimal Amount { get; set; }

    /// <summary>Card, EFT or Cash.</summary>
    /// <example>Card</example>
    [Required]
    public PaymentMethod? PaymentMethod { get; set; }
}
