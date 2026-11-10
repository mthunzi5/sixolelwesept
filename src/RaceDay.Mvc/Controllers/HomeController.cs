using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Auth;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>Public pages: upcoming events, event details and leaderboards. Also the friendly error pages.</summary>
public class HomeController : RaceDayControllerBase
{
    private readonly RaceDayApiClient _api;

    public HomeController(RaceDayApiClient api)
    {
        _api = api;
    }

    /// <summary>Upcoming events, filtered by type, date range and a search term.</summary>
    public async Task<IActionResult> Index(EventFilter filter)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = filter.From is DateTime f && DateOnly.FromDateTime(f) > today ? DateOnly.FromDateTime(f) : today;
        DateOnly? to = filter.To is DateTime t ? DateOnly.FromDateTime(t) : null;

        var model = new HomeViewModel { Filter = filter };
        if (to is not null && to < from)
        {
            FlashError("The 'To' date must be on or after the 'From' date.");
            return View(model);
        }

        var result = await _api.GetEventsAsync(EventStatus.Scheduled, filter.EventType, from, to);
        if (!result.Success)
        {
            FlashError(result.Error);
            return View(model);
        }

        var events = result.Data ?? new List<EventDto>();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            events = events.Where(e =>
                e.EventName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || e.Location.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (e.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }

        model.Events = events;
        return View(model);
    }

    /// <summary>Full event information with its categories, and enrol buttons for Participants.</summary>
    public async Task<IActionResult> Event(int id)
    {
        var result = await _api.GetEventAsync(id);
        if (result.StatusCode == StatusCodes.Status404NotFound) return NotFound();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error);
            return RedirectToAction(nameof(Index));
        }

        var model = new EventDetailsViewModel
        {
            Event = result.Data,
            IsOwner = HttpContext.Session.IsOrganiser() && result.Data.OrganiserId == CurrentUserId
        };

        if (HttpContext.Session.IsParticipant())
        {
            var mine = await _api.GetMyEnrolmentsAsync();
            if (RedirectForAuthFailure(mine) is IActionResult redirect) return redirect;
            model.MyEnrolment = mine.Data?.FirstOrDefault(en => en.EventId == id && en.Status != EnrolmentStatus.Withdrawn);
        }

        return View(model);
    }

    /// <summary>Public results for an event.</summary>
    public async Task<IActionResult> Leaderboard(int id)
    {
        var ev = await _api.GetEventAsync(id);
        if (ev.StatusCode == StatusCodes.Status404NotFound) return NotFound();
        if (!ev.Success || ev.Data is null)
        {
            FlashError(ev.Error);
            return RedirectToAction(nameof(Index));
        }

        var results = await _api.GetEventResultsAsync(id);
        if (!results.Success) FlashError(results.Error);

        return View(new LeaderboardViewModel { Event = ev.Data, Results = results.Data ?? new List<ResultDto>() });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }

    [Route("Home/StatusCode")]
    public IActionResult StatusCodePage(int code)
    {
        Response.StatusCode = code;
        return View("StatusCode", code);
    }
}
