using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Event entries. Participants enrol by choosing a category, and Organisers view the entrants for their events.</summary>
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Enrol the logged-in Participant in an event by choosing one of its categories.</summary>
    /// <remarks>
    /// The participant comes from the session, so no request body is needed. The API records the link
    /// between the Participant, the category and its event, and assigns a unique bib number.
    /// The enrolment is <c>Pending</c> until the entry fee is paid (POST /api/enrolments/{id}/payment), or
    /// <c>Confirmed</c> straight away for free categories.
    ///
    /// Rules: one category per participant per event, the category must not be full, and the event must still be Scheduled.
    /// Re-enrolling after withdrawing reactivates the original enrolment.
    /// </remarks>
    /// <response code="201">The new enrolment, including its bib number.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    /// <response code="404">No category with that ID.</response>
    /// <response code="409">Already enrolled in this event, the category is full, or the event is not open for entries.</response>
    [HttpPost("api/categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int categoryId)
    {
        var category = await _db.Categories.Include(c => c.Event).Include(c => c.Enrolments)
            .SingleOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null) return NotFoundProblem($"Category {categoryId} was not found.");

        if (category.Event!.Status != EventStatus.Scheduled)
        {
            return ConflictProblem($"'{category.Event.EventName}' is {category.Event.Status} and no longer accepting entries.");
        }

        var participantId = CurrentUserId;
        var existingInEvent = await _db.Enrolments.Include(en => en.Category)
            .Where(en => en.ParticipantId == participantId && en.Category!.EventId == category.EventId)
            .ToListAsync();

        if (existingInEvent.FirstOrDefault(en => en.Status != EnrolmentStatus.Withdrawn) is Enrolment active)
        {
            return ConflictProblem(active.CategoryId == categoryId
                ? "You are already enrolled in this category."
                : $"You are already enrolled in '{active.Category!.CategoryName}' for this event. Withdraw from it first to change category.");
        }

        if (category.Enrolments.Count(en => en.Status != EnrolmentStatus.Withdrawn) >= category.MaxParticipants)
        {
            return ConflictProblem($"'{category.CategoryName}' is full ({category.MaxParticipants} participants).");
        }

        var status = category.EntryFee > 0 ? EnrolmentStatus.Pending : EnrolmentStatus.Confirmed;

        // The unique (ParticipantID, CategoryID) key means a withdrawn entry in this same
        // category is brought back rather than inserted again.
        var enrolment = existingInEvent.FirstOrDefault(en => en.CategoryId == categoryId);
        if (enrolment is not null)
        {
            enrolment.Status = status;
            enrolment.EnrolmentDate = DateTime.UtcNow;
        }
        else
        {
            enrolment = new Enrolment
            {
                ParticipantId = participantId,
                CategoryId = categoryId,
                BibNumber = await NextBibNumberAsync(category.EventId),
                Status = status
            };
            _db.Enrolments.Add(enrolment);
        }

        await _db.SaveChangesAsync();
        var created = await LoadAsync(enrolment.EnrolmentId);
        return CreatedAtAction(nameof(GetEnrolment), new { id = enrolment.EnrolmentId }, created!.ToDto());
    }

    /// <summary>List the logged-in Participant's own enrolments.</summary>
    /// <response code="200">The participant's enrolments, most recent first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("api/participants/me/enrolments")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyEnrolments()
    {
        var enrolments = await Query()
            .Where(en => en.ParticipantId == CurrentUserId)
            .OrderByDescending(en => en.EnrolmentDate)
            .ToListAsync();
        return Ok(enrolments.Select(en => en.ToDto()));
    }

    /// <summary>List everyone enrolled in a category (the roster). Only the Organiser who created the event may do this.</summary>
    /// <response code="200">The category's enrolments, ordered by bib number.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or as a different Organiser.</response>
    /// <response code="404">No category with that ID.</response>
    [HttpGet("api/categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryEnrolments(int categoryId)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null) return NotFoundProblem($"Category {categoryId} was not found.");
        if (category.Event!.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only view enrolments for events you created.");

        var enrolments = await Query().Where(en => en.CategoryId == categoryId).OrderBy(en => en.BibNumber).ToListAsync();
        return Ok(enrolments.Select(en => en.ToDto()));
    }

    /// <summary>Get a single enrolment. Allowed for the Participant who owns it or the Organiser of its event.</summary>
    /// <response code="200">The enrolment.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Neither the enrolled participant nor the event's organiser.</response>
    /// <response code="404">No enrolment with that ID.</response>
    [HttpGet("api/enrolments/{id:int}")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrolment(int id)
    {
        var enrolment = await LoadAsync(id);
        if (enrolment is null) return NotFoundProblem($"Enrolment {id} was not found.");
        if (!CanAccess(enrolment)) return ForbiddenProblem("You can only view your own enrolments, or enrolments for events you organise.");

        return Ok(enrolment.ToDto());
    }

    /// <summary>Withdraw an enrolment. Allowed for the Participant who owns it or the Organiser of its event.</summary>
    /// <remarks>
    /// The enrolment row is kept with status <c>Withdrawn</c> so the bib number is never reused, and any paid
    /// entry fee is marked Refunded. You cannot withdraw once a result has been captured.
    /// </remarks>
    /// <response code="204">Enrolment withdrawn.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Neither the enrolled participant nor the event's organiser.</response>
    /// <response code="404">No enrolment with that ID.</response>
    /// <response code="409">A result has already been captured, or the enrolment is already withdrawn.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var enrolment = await LoadAsync(id);
        if (enrolment is null) return NotFoundProblem($"Enrolment {id} was not found.");
        if (!CanAccess(enrolment)) return ForbiddenProblem("You can only withdraw your own enrolments, or enrolments for events you organise.");

        if (enrolment.Result is not null) return ConflictProblem("A result has already been captured for this enrolment.");
        if (enrolment.Status == EnrolmentStatus.Withdrawn) return ConflictProblem("This enrolment is already withdrawn.");

        enrolment.Status = EnrolmentStatus.Withdrawn;
        if (enrolment.Payment is { Status: PaymentStatus.Paid } payment)
        {
            payment.Status = PaymentStatus.Refunded;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<Enrolment> Query() => _db.Enrolments
        .Include(en => en.Participant)
        .Include(en => en.Category).ThenInclude(c => c!.Event)
        .Include(en => en.Result)
        .Include(en => en.Payment);

    private Task<Enrolment?> LoadAsync(int id) => Query().SingleOrDefaultAsync(en => en.EnrolmentId == id);

    private bool CanAccess(Enrolment enrolment) => CurrentRole == UserRole.Participant
        ? enrolment.ParticipantId == CurrentUserId
        : enrolment.Category!.Event!.OrganiserId == CurrentUserId;

    /// <summary>Bib numbers look like E12-0007: the event ID plus a running number within that event.</summary>
    private async Task<string> NextBibNumberAsync(int eventId)
    {
        var sequence = await _db.Enrolments.CountAsync(en => en.Category!.EventId == eventId) + 1;
        string bib;
        do
        {
            bib = $"E{eventId}-{sequence++:D4}";
        } while (await _db.Enrolments.AnyAsync(en => en.BibNumber == bib));
        return bib;
    }
}
