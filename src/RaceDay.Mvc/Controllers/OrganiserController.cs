using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Auth;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>Organiser-only pages: dashboard, event management, categories, enrolments and results entry.</summary>
[RequireRole(UserRole.Organiser)]
public class OrganiserController : RaceDayControllerBase
{
    private readonly RaceDayApiClient _api;

    public OrganiserController(RaceDayApiClient api)
    {
        _api = api;
    }

    // ---------- Dashboard & events ----------

    /// <summary>Summary of the organiser's events: totals, enrolments per event and upcoming dates.</summary>
    public async Task<IActionResult> Dashboard()
    {
        var result = await _api.GetMyEventsAsync();
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;
        if (!result.Success) FlashError(result.Error);

        return View(new OrganiserDashboardViewModel { Events = result.Data ?? new List<EventDto>() });
    }

    public async Task<IActionResult> Events()
    {
        var result = await _api.GetMyEventsAsync();
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;
        if (!result.Success) FlashError(result.Error);

        return View((result.Data ?? new List<EventDto>()).OrderByDescending(e => e.EventDate).ToList());
    }

    [HttpGet]
    public IActionResult Create() => View("EventForm", new EventForm { EventDate = DateTime.Today.AddMonths(1) });

    [HttpPost]
    public async Task<IActionResult> Create(EventForm form)
    {
        if (!ModelState.IsValid) return View("EventForm", form);

        var result = await _api.CreateEventAsync(form);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;
        if (!result.Success || result.Data is null)
        {
            AddApiError(result);
            return View("EventForm", form);
        }

        var created = result.Data;
        await UploadBannerIfProvided(created.EventId, form.BannerImage);
        FlashSuccess($"'{created.EventName}' has been created. Now add its categories.");
        return RedirectToAction(nameof(Categories), new { id = created.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (ev, redirect) = await LoadOwnEventAsync(id);
        if (redirect is not null) return redirect;

        return View("EventForm", new EventForm
        {
            EventId = ev!.EventId,
            EventName = ev.EventName,
            Description = ev.Description,
            EventDate = ev.EventDate.ToDateTime(TimeOnly.MinValue),
            Location = ev.Location,
            DistanceKm = ev.DistanceKm,
            EventType = ev.EventType,
            Status = ev.Status,
            CurrentBannerUrl = ev.BannerImageUrl
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, EventForm form)
    {
        form.EventId = id;
        if (!ModelState.IsValid) return View("EventForm", form);

        var result = await _api.UpdateEventAsync(id, form);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;
        if (!result.Success)
        {
            AddApiError(result);
            return View("EventForm", form);
        }

        await UploadBannerIfProvided(id, form.BannerImage);
        FlashSuccess("Event updated.");
        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _api.DeleteEventAsync(id);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;

        if (result.Success) FlashSuccess("Event deleted.");
        else FlashError(result.Error);
        return RedirectToAction(nameof(Events));
    }

    // ---------- Categories ----------

    public async Task<IActionResult> Categories(int id)
    {
        var (ev, redirect) = await LoadOwnEventAsync(id);
        if (redirect is not null) return redirect;

        return View(new CategoriesViewModel { Event = ev! });
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(int id, [Bind(Prefix = "Form")] CategoryForm form)
    {
        if (form.CategoryType == CategoryType.Age && form.MinAge is null && form.MaxAge is null)
        {
            ModelState.AddModelError("Form.MaxAge", "Age categories need a minimum age, a maximum age, or both.");
        }

        if (ModelState.IsValid)
        {
            var result = await _api.CreateCategoryAsync(id, form);
            if (RedirectForAuthFailure(result) is IActionResult authRedirect) return authRedirect;
            if (result.Success)
            {
                FlashSuccess($"Category '{form.CategoryName}' added.");
                return RedirectToAction(nameof(Categories), new { id });
            }

            AddApiError(result);
        }

        var (ev, redirect) = await LoadOwnEventAsync(id);
        if (redirect is not null) return redirect;
        return View(nameof(Categories), new CategoriesViewModel { Event = ev!, Form = form });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id, int categoryId)
    {
        var result = await _api.DeleteCategoryAsync(categoryId);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;

        if (result.Success) FlashSuccess("Category removed.");
        else FlashError(result.Error);
        return RedirectToAction(nameof(Categories), new { id });
    }

    // ---------- Enrolments ----------

    /// <summary>Everyone enrolled in the event, with their category and enrolment status.</summary>
    public async Task<IActionResult> Enrolments(int id, int? categoryId)
    {
        var (ev, redirect) = await LoadOwnEventAsync(id);
        if (redirect is not null) return redirect;

        var result = await _api.GetEventEnrolmentsAsync(id);
        if (RedirectForAuthFailure(result) is IActionResult authRedirect) return authRedirect;
        if (!result.Success) FlashError(result.Error);

        var enrolments = result.Data ?? new List<EnrolmentDto>();
        if (categoryId is not null) enrolments = enrolments.Where(e => e.CategoryId == categoryId).ToList();

        return View(new EnrolmentsViewModel { Event = ev!, Enrolments = enrolments, CategoryId = categoryId });
    }

    // ---------- Results ----------

    /// <summary>Capture finish times and positions for each entrant once the event has happened.</summary>
    public async Task<IActionResult> Results(int id)
    {
        var (ev, redirect) = await LoadOwnEventAsync(id);
        if (redirect is not null) return redirect;

        var result = await _api.GetEventEnrolmentsAsync(id);
        if (RedirectForAuthFailure(result) is IActionResult authRedirect) return authRedirect;
        if (!result.Success) FlashError(result.Error);

        var entrants = (result.Data ?? new List<EnrolmentDto>())
            .Where(e => e.Status != EnrolmentStatus.Withdrawn)
            .OrderBy(e => e.CategoryName)
            .ThenBy(e => e.Position ?? int.MaxValue)
            .ThenBy(e => e.BibNumber)
            .ToList();

        return View(new ResultsEntryViewModel { Event = ev!, Entrants = entrants });
    }

    [HttpPost]
    public async Task<IActionResult> SaveResult(int id, ResultForm form)
    {
        if (!ModelState.IsValid)
        {
            FlashError(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return RedirectToAction(nameof(Results), new { id });
        }

        int? seconds = null;
        if (!string.IsNullOrWhiteSpace(form.FinishTime))
        {
            seconds = ParseFinishTime(form.FinishTime);
            if (seconds is null)
            {
                FlashError("Enter the finish time as h:mm:ss, e.g. 1:05:30.");
                return RedirectToAction(nameof(Results), new { id });
            }
        }

        // DNF and DSQ entrants don't get a finishing position.
        var position = form.Status == ResultStatus.Finished ? form.Position : null;

        var result = form.ResultId is int resultId
            ? await _api.UpdateResultAsync(resultId, seconds, position, form.Status)
            : await _api.CaptureResultAsync(form.EnrolmentId, seconds, position, form.Status);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;

        if (result.Success && result.Data is ResultDto saved)
        {
            FlashSuccess($"Result saved for {saved.ParticipantName} (bib {saved.BibNumber}).");
        }
        else
        {
            FlashError(result.Error);
        }

        return RedirectToAction(nameof(Results), new { id });
    }

    /// <summary>Marks the event Completed so its results are published on the public leaderboard.</summary>
    [HttpPost]
    public async Task<IActionResult> PublishResults(int id)
    {
        var (ev, redirect) = await LoadOwnEventAsync(id);
        if (redirect is not null) return redirect;

        var result = await _api.SetEventStatusAsync(ev!, EventStatus.Completed);
        if (RedirectForAuthFailure(result) is IActionResult authRedirect) return authRedirect;

        if (result.Success) FlashSuccess($"Results for '{ev!.EventName}' are published and the event is marked Completed.");
        else FlashError(result.Error);
        return RedirectToAction(nameof(Results), new { id });
    }

    // ---------- Helpers ----------

    /// <summary>Loads an event and makes sure it belongs to the signed-in organiser.</summary>
    private async Task<(EventDto? Event, IActionResult? Redirect)> LoadOwnEventAsync(int id)
    {
        var result = await _api.GetEventAsync(id);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return (null, redirect);
        if (result.StatusCode == StatusCodes.Status404NotFound) return (null, NotFound());
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error);
            return (null, RedirectToAction(nameof(Events)));
        }

        if (result.Data.OrganiserId != CurrentUserId)
        {
            FlashError("You can only manage events you created.");
            return (null, RedirectToAction("AccessDenied", "Auth"));
        }

        return (result.Data, null);
    }

    /// <summary>Sends a banner to the API (which stores it in Azure Blob Storage). A failed upload doesn't undo the event save.</summary>
    private async Task UploadBannerIfProvided(int eventId, IFormFile? banner)
    {
        if (banner is null || banner.Length == 0) return;

        var upload = await _api.UploadBannerAsync(eventId, banner);
        if (!upload.Success)
        {
            TempData["Info"] = $"The event was saved, but the banner couldn't be uploaded: {upload.Error}";
        }
    }

    /// <summary>"1:05:30" → 3930, "52:05" → 3125. Returns null if the text isn't a valid time.</summary>
    private static int? ParseFinishTime(string text)
    {
        var parts = text.Trim().Split(':');
        if (parts.Length is < 2 or > 3) return null;

        var numbers = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return null;
        }

        var (h, m, s) = parts.Length == 3 ? (numbers[0], numbers[1], numbers[2]) : (0, numbers[0], numbers[1]);
        if (m > 59 || s > 59) return null;

        var total = h * 3600 + m * 60 + s;
        return total > 0 ? total : null;
    }
}
