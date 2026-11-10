using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Events. Anyone can browse them, but only Organisers can create, update and delete their own events.</summary>
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>List events, with optional filters.</summary>
    /// <param name="status">Only events with this status (Scheduled, Ongoing, Completed, Cancelled).</param>
    /// <param name="eventType">Only events of this type (Run, Walk, Cycle).</param>
    /// <param name="from">Only events on or after this date (yyyy-MM-dd).</param>
    /// <param name="to">Only events on or before this date (yyyy-MM-dd).</param>
    /// <response code="200">The matching events, soonest first.</response>
    [HttpGet("api/events")]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] EventStatus? status, [FromQuery] EventType? eventType,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var query = _db.Events.Include(e => e.Organiser).Include(e => e.Categories).ThenInclude(c => c.Enrolments).AsQueryable();

        if (status is not null) query = query.Where(e => e.Status == status);
        if (eventType is not null) query = query.Where(e => e.EventType == eventType);
        if (from is not null) query = query.Where(e => e.EventDate >= from);
        if (to is not null) query = query.Where(e => e.EventDate <= to);

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToDto()));
    }

    /// <summary>Get one event with its categories.</summary>
    /// <response code="200">The event and its categories.</response>
    /// <response code="404">No event with that ID.</response>
    [HttpGet("api/events/{id:int}")]
    [ProducesResponseType(typeof(EventDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Categories).ThenInclude(c => c.Enrolments)
            .SingleOrDefaultAsync(e => e.EventId == id);

        return ev is null ? NotFoundProblem($"Event {id} was not found.") : Ok(ev.ToDetailDto());
    }

    /// <summary>List the events created by the logged-in Organiser.</summary>
    /// <response code="200">The organiser's events, soonest first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpGet("api/organisers/me/events")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyEvents()
    {
        var events = await _db.Events.Include(e => e.Organiser).Include(e => e.Categories).ThenInclude(c => c.Enrolments)
            .Where(e => e.OrganiserId == CurrentUserId)
            .OrderBy(e => e.EventDate)
            .ToListAsync();
        return Ok(events.Select(e => e.ToDto()));
    }

    /// <summary>Create a new event owned by the logged-in Organiser.</summary>
    /// <remarks>The event date cannot be in the past. Status defaults to Scheduled.</remarks>
    /// <response code="201">The created event.</response>
    /// <response code="400">Validation failed, for example a missing field, a past date or a non-positive distance.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpPost("api/events")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEvent(EventRequest request)
    {
        if (request.EventDate < DateOnly.FromDateTime(DateTime.Today))
        {
            return BadRequestProblem("The event date cannot be in the past.");
        }

        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            EventName = request.EventName.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = request.EventType!.Value,
            Status = request.Status ?? EventStatus.Scheduled
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        await _db.Entry(ev).Reference(e => e.Organiser).LoadAsync();

        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, ev.ToDto());
    }

    /// <summary>Update an event. Only the Organiser who created it may do this.</summary>
    /// <remarks>If status is left out, the current status is kept.</remarks>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or as a different Organiser.</response>
    /// <response code="404">No event with that ID.</response>
    [HttpPut("api/events/{id:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, EventRequest request)
    {
        var ev = await _db.Events.Include(e => e.Organiser).Include(e => e.Categories).ThenInclude(c => c.Enrolments).SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} was not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only update events you created.");

        ev.EventName = request.EventName.Trim();
        ev.Description = request.Description?.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = request.EventType!.Value;
        ev.Status = request.Status ?? ev.Status;

        await _db.SaveChangesAsync();
        return Ok(ev.ToDto());
    }

    /// <summary>Delete an event and its categories. Only the Organiser who created it may do this.</summary>
    /// <remarks>An event that already has enrolments cannot be deleted. Set its status to Cancelled instead.</remarks>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or as a different Organiser.</response>
    /// <response code="404">No event with that ID.</response>
    /// <response code="409">The event already has enrolments.</response>
    [HttpDelete("api/events/{id:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events.Include(e => e.Categories).SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} was not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only delete events you created.");

        if (await _db.Enrolments.AnyAsync(en => en.Category!.EventId == id))
        {
            return ConflictProblem("This event already has enrolments and cannot be deleted. Set its status to Cancelled instead.");
        }

        _db.Categories.RemoveRange(ev.Categories);
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>List every enrolment across all categories of an event. Only the Organiser who created the event may do this.</summary>
    /// <response code="200">All enrolments for the event, grouped by category and ordered by bib number.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or as a different Organiser.</response>
    /// <response code="404">No event with that ID.</response>
    [HttpGet("api/events/{id:int}/enrolments")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev is null) return NotFoundProblem($"Event {id} was not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only view enrolments for events you created.");

        var enrolments = await _db.Enrolments
            .Include(en => en.Participant)
            .Include(en => en.Result)
            .Include(en => en.Payment)
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .Where(en => en.Category!.EventId == id)
            .OrderBy(en => en.Category!.CategoryName).ThenBy(en => en.BibNumber)
            .ToListAsync();

        return Ok(enrolments.Select(en => en.ToDto()));
    }
}
