using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Race results. Organisers capture finish times and positions after an event, and Participants view their own results.</summary>
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Public results (leaderboard) for an event.</summary>
    /// <response code="200">Results grouped by category, finishers by position first, then DNF/DSQ.</response>
    /// <response code="404">No event with that ID.</response>
    [HttpGet("api/events/{eventId:int}/results")]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventResults(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return NotFoundProblem($"Event {eventId} was not found.");
        }

        var results = await Query().Where(r => r.Enrolment!.Category!.EventId == eventId).ToListAsync();
        return Ok(await ToDtosAsync(Rank(results)));
    }

    /// <summary>List the logged-in Participant's own results across all events.</summary>
    /// <response code="200">The participant's results, most recent event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("api/participants/me/results")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyResults()
    {
        var results = await Query()
            .Where(r => r.Enrolment!.ParticipantId == CurrentUserId)
            .OrderByDescending(r => r.Enrolment!.Category!.Event!.EventDate)
            .ToListAsync();
        return Ok(await ToDtosAsync(results));
    }

    /// <summary>Capture the finish time and position for an enrolment. Only the Organiser of the event may do this.</summary>
    /// <remarks>
    /// Allowed only once the event date has arrived. A <c>Finished</c> result needs both finishTimeSeconds and position,
    /// and each position can only be used once per category. A <c>DNF</c> or <c>DSQ</c> result must have no position.
    /// Use PUT /api/results/{id} to correct a result that has already been captured.
    /// </remarks>
    /// <response code="201">The captured result.</response>
    /// <response code="400">Invalid combination of status, time and position.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or as a different Organiser.</response>
    /// <response code="404">No enrolment with that ID.</response>
    /// <response code="409">A result already exists, the enrolment was withdrawn, the event hasn't happened yet, or the position is taken.</response>
    [HttpPost("api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CaptureResult(int enrolmentId, ResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .Include(en => en.Result)
            .SingleOrDefaultAsync(en => en.EnrolmentId == enrolmentId);
        if (enrolment is null) return NotFoundProblem($"Enrolment {enrolmentId} was not found.");

        var ev = enrolment.Category!.Event!;
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only capture results for events you organise.");

        if (enrolment.Result is not null) return ConflictProblem("A result has already been captured for this enrolment. Use PUT /api/results/{id} to correct it.");
        if (enrolment.Status == EnrolmentStatus.Withdrawn) return ConflictProblem("This participant withdrew from the event.");
        if (ev.EventDate > DateOnly.FromDateTime(DateTime.Today)) return ConflictProblem($"Results can only be captured once the event has taken place ({ev.EventDate:yyyy-MM-dd}).");

        if (ValidateRequest(request) is string error) return BadRequestProblem(error);
        if (await PositionTakenAsync(enrolment.CategoryId, request.Position, excludeResultId: null))
        {
            return ConflictProblem($"Position {request.Position} is already taken in this category.");
        }

        var result = new Result { EnrolmentId = enrolmentId };
        Apply(result, request);
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var saved = await Query().SingleAsync(r => r.ResultId == result.ResultId);
        return Created($"/api/results/{result.ResultId}", (await ToDtosAsync([saved])).Single());
    }

    /// <summary>Correct a result that has already been captured. Only the Organiser of the event may do this.</summary>
    /// <response code="200">The corrected result.</response>
    /// <response code="400">Invalid combination of status, time and position.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or as a different Organiser.</response>
    /// <response code="404">No result with that ID.</response>
    /// <response code="409">The position is already taken by another participant in this category.</response>
    [HttpPut("api/results/{id:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateResult(int id, ResultRequest request)
    {
        var result = await Query().SingleOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFoundProblem($"Result {id} was not found.");
        if (result.Enrolment!.Category!.Event!.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only correct results for events you organise.");

        if (ValidateRequest(request) is string error) return BadRequestProblem(error);
        if (await PositionTakenAsync(result.Enrolment.CategoryId, request.Position, excludeResultId: id))
        {
            return ConflictProblem($"Position {request.Position} is already taken in this category.");
        }

        Apply(result, request);
        await _db.SaveChangesAsync();
        return Ok((await ToDtosAsync([result])).Single());
    }

    private IQueryable<Result> Query() => _db.Results
        .Include(r => r.Enrolment).ThenInclude(en => en!.Participant)
        .Include(r => r.Enrolment).ThenInclude(en => en!.Category).ThenInclude(c => c!.Event);

    /// <summary>Maps results to DTOs and fills in how many Finished results each one's category has.</summary>
    private async Task<List<ResultDto>> ToDtosAsync(IEnumerable<Result> results)
    {
        var list = results.ToList();
        var categoryIds = list.Select(r => r.Enrolment!.CategoryId).Distinct().ToList();
        var finishers = await _db.Results
            .Where(r => r.Status == ResultStatus.Finished && categoryIds.Contains(r.Enrolment!.CategoryId))
            .GroupBy(r => r.Enrolment!.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

        return list.Select(r =>
        {
            var dto = r.ToDto();
            dto.FinishersInCategory = finishers.GetValueOrDefault(r.Enrolment!.CategoryId);
            return dto;
        }).ToList();
    }

    private static IEnumerable<Result> Rank(IEnumerable<Result> results) => results
        .OrderBy(r => r.Enrolment!.Category!.DistanceKm)
        .ThenBy(r => r.Enrolment!.Category!.CategoryName)
        .ThenBy(r => r.Status == ResultStatus.Finished ? 0 : 1)
        .ThenBy(r => r.Position ?? int.MaxValue);

    private static string? ValidateRequest(ResultRequest request)
    {
        if (!Enum.IsDefined(request.Status)) return "Status must be Finished, DNF or DSQ.";

        if (request.Status == ResultStatus.Finished && (request.FinishTimeSeconds is null || request.Position is null))
        {
            return "A Finished result needs both finishTimeSeconds and position.";
        }

        if (request.Status != ResultStatus.Finished && request.Position is not null)
        {
            return $"A {request.Status} result cannot have a finishing position.";
        }

        return null;
    }

    private Task<bool> PositionTakenAsync(int categoryId, int? position, int? excludeResultId) =>
        position is null
            ? Task.FromResult(false)
            : _db.Results.AnyAsync(r => r.Enrolment!.CategoryId == categoryId && r.Position == position && r.ResultId != excludeResultId);

    private static void Apply(Result result, ResultRequest request)
    {
        result.FinishTimeSeconds = request.FinishTimeSeconds;
        result.Position = request.Position;
        result.Status = request.Status;
    }
}
