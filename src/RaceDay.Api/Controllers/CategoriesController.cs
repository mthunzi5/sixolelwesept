using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Age or distance categories within an event. Anyone can view them, but only the event's Organiser can manage them.</summary>
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>List the categories of an event.</summary>
    /// <response code="200">The event's categories, shortest distance first.</response>
    /// <response code="404">No event with that ID.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoriesForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return NotFoundProblem($"Event {eventId} was not found.");
        }

        var categories = await _db.Categories.Include(c => c.Enrolments)
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.DistanceKm).ThenBy(c => c.CategoryName)
            .ToListAsync();
        return Ok(categories.Select(c => c.ToDto()));
    }

    /// <summary>Get a single category.</summary>
    /// <response code="200">The category.</response>
    /// <response code="404">No category with that ID.</response>
    [HttpGet("api/categories/{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Enrolments).SingleOrDefaultAsync(c => c.CategoryId == id);
        return category is null ? NotFoundProblem($"Category {id} was not found.") : Ok(category.ToDto());
    }

    /// <summary>Add a category to an event. Only the Organiser who created the event may do this.</summary>
    /// <remarks>
    /// Use <c>categoryType: "Distance"</c> for categories such as "10km" or "21km", or
    /// <c>categoryType: "Age"</c> with <c>minAge</c> and/or <c>maxAge</c> for categories such as "Under 20" or "Senior".
    /// Category names must be unique within an event.
    /// </remarks>
    /// <response code="201">The created category.</response>
    /// <response code="400">Validation failed, for example an age category with no age bounds, or minAge greater than maxAge.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or as a different Organiser.</response>
    /// <response code="404">No event with that ID.</response>
    /// <response code="409">The event already has a category with this name.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory(int eventId, CategoryRequest request)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev is null) return NotFoundProblem($"Event {eventId} was not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only add categories to events you created.");

        if (ValidateAges(request) is string ageError) return BadRequestProblem(ageError);

        var name = request.CategoryName.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == eventId && c.CategoryName == name))
        {
            return ConflictProblem($"This event already has a category named '{name}'.");
        }

        var category = new Category { EventId = eventId };
        Apply(category, request);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCategory), new { id = category.CategoryId }, category.ToDto());
    }

    /// <summary>Update a category. Only the Organiser who created the event may do this.</summary>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or as a different Organiser.</response>
    /// <response code="404">No category with that ID.</response>
    /// <response code="409">The new name clashes with another category, or maxParticipants is lower than the current number of entrants.</response>
    [HttpPut("api/categories/{id:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategory(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).Include(c => c.Enrolments)
            .SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundProblem($"Category {id} was not found.");
        if (category.Event!.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only update categories of events you created.");

        if (ValidateAges(request) is string ageError) return BadRequestProblem(ageError);

        var name = request.CategoryName.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == category.EventId && c.CategoryName == name && c.CategoryId != id))
        {
            return ConflictProblem($"This event already has a category named '{name}'.");
        }

        var activeEntrants = category.Enrolments.Count(en => en.Status != EnrolmentStatus.Withdrawn);
        if (request.MaxParticipants < activeEntrants)
        {
            return ConflictProblem($"maxParticipants cannot be lower than the {activeEntrants} participants already enrolled.");
        }

        Apply(category, request);
        await _db.SaveChangesAsync();
        return Ok(category.ToDto());
    }

    /// <summary>Delete a category. Only the Organiser who created the event may do this.</summary>
    /// <response code="204">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or as a different Organiser.</response>
    /// <response code="404">No category with that ID.</response>
    /// <response code="409">The category already has enrolments.</response>
    [HttpDelete("api/categories/{id:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundProblem($"Category {id} was not found.");
        if (category.Event!.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only delete categories of events you created.");

        if (await _db.Enrolments.AnyAsync(en => en.CategoryId == id))
        {
            return ConflictProblem("This category already has enrolments and cannot be deleted.");
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? ValidateAges(CategoryRequest request)
    {
        if (request.CategoryType == CategoryType.Age && request.MinAge is null && request.MaxAge is null)
        {
            return "Age categories need a minAge, a maxAge, or both (e.g. Under 20 has maxAge 19).";
        }

        if (request.MinAge is int min && request.MaxAge is int max && min > max)
        {
            return "minAge cannot be greater than maxAge.";
        }

        return null;
    }

    private static void Apply(Category category, CategoryRequest request)
    {
        category.CategoryName = request.CategoryName.Trim();
        category.CategoryType = request.CategoryType;
        category.DistanceKm = request.DistanceKm;
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.EntryFee = request.EntryFee;
        category.MaxParticipants = request.MaxParticipants;
    }
}
