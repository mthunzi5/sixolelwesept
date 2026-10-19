using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Entry-fee payments for enrolments.</summary>
public class PaymentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public PaymentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Pay the entry fee for one of the logged-in Participant's enrolments.</summary>
    /// <remarks>The amount must equal the category's entry fee. A successful payment changes the enrolment from Pending to Confirmed.</remarks>
    /// <response code="201">The payment record.</response>
    /// <response code="400">The amount does not match the entry fee, or the payment method is invalid.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser, or the enrolment belongs to someone else.</response>
    /// <response code="404">No enrolment with that ID.</response>
    /// <response code="409">Already paid, or the enrolment was withdrawn.</response>
    [HttpPost("api/enrolments/{enrolmentId:int}/payment")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Pay(int enrolmentId, PaymentRequest request)
    {
        var enrolment = await _db.Enrolments.Include(en => en.Category).Include(en => en.Payment)
            .SingleOrDefaultAsync(en => en.EnrolmentId == enrolmentId);
        if (enrolment is null) return NotFoundProblem($"Enrolment {enrolmentId} was not found.");
        if (enrolment.ParticipantId != CurrentUserId) return ForbiddenProblem("You can only pay for your own enrolments.");

        if (enrolment.Status == EnrolmentStatus.Withdrawn) return ConflictProblem("This enrolment was withdrawn.");
        if (enrolment.Payment is { Status: PaymentStatus.Paid }) return ConflictProblem("This enrolment has already been paid for.");

        if (request.PaymentMethod is not PaymentMethod method || !Enum.IsDefined(method))
        {
            return BadRequestProblem("paymentMethod must be Card, EFT or Cash.");
        }

        if (request.Amount != enrolment.Category!.EntryFee)
        {
            return BadRequestProblem($"The entry fee for '{enrolment.Category.CategoryName}' is {enrolment.Category.EntryFee:0.00}.");
        }

        // A refunded payment (from an earlier withdrawal) is reused: there is one payment row per enrolment.
        var payment = enrolment.Payment ?? new Payment { EnrolmentId = enrolmentId };
        payment.Amount = request.Amount;
        payment.PaymentMethod = method;
        payment.PaymentDate = DateTime.UtcNow;
        payment.Status = PaymentStatus.Paid;
        if (enrolment.Payment is null) _db.Payments.Add(payment);

        enrolment.Status = EnrolmentStatus.Confirmed;
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPayment), new { enrolmentId }, payment.ToDto());
    }

    /// <summary>Get the payment status of an enrolment. Allowed for the Participant who owns it or the Organiser of its event.</summary>
    /// <response code="200">The payment record.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Neither the enrolled participant nor the event's organiser.</response>
    /// <response code="404">No enrolment with that ID, or no payment made yet.</response>
    [HttpGet("api/enrolments/{enrolmentId:int}/payment")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(int enrolmentId)
    {
        var enrolment = await _db.Enrolments
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .Include(en => en.Payment)
            .SingleOrDefaultAsync(en => en.EnrolmentId == enrolmentId);
        if (enrolment is null) return NotFoundProblem($"Enrolment {enrolmentId} was not found.");

        var allowed = CurrentRole == UserRole.Participant
            ? enrolment.ParticipantId == CurrentUserId
            : enrolment.Category!.Event!.OrganiserId == CurrentUserId;
        if (!allowed) return ForbiddenProblem("You can only view payments for your own enrolments, or for events you organise.");

        return enrolment.Payment is null
            ? NotFoundProblem("No payment has been made for this enrolment yet.")
            : Ok(enrolment.Payment.ToDto());
    }
}
