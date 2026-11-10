using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Auth;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>Participant-only pages: enrolling, my enrolments (with payment) and my results.</summary>
[RequireRole(UserRole.Participant)]
public class ParticipantController : RaceDayControllerBase
{
    private readonly RaceDayApiClient _api;

    public ParticipantController(RaceDayApiClient api)
    {
        _api = api;
    }

    /// <summary>Enrols the participant in the chosen category. The API records it straight away.</summary>
    [HttpPost]
    public async Task<IActionResult> Enrol(int categoryId, int eventId)
    {
        var result = await _api.EnrolAsync(categoryId);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;

        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error);
            return RedirectToAction("Event", "Home", new { id = eventId });
        }

        var en = result.Data;
        FlashSuccess(en.Status == EnrolmentStatus.Pending
            ? $"You're entered in {en.CategoryName} for {en.EventName} with bib {en.BibNumber}. Pay the entry fee below to confirm your place."
            : $"You're confirmed in {en.CategoryName} for {en.EventName}. Your bib is {en.BibNumber}.");
        return RedirectToAction(nameof(MyEnrolments));
    }

    /// <summary>Every event the participant has enrolled for, with its status.</summary>
    public async Task<IActionResult> MyEnrolments()
    {
        var result = await _api.GetMyEnrolmentsAsync();
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;
        if (!result.Success) FlashError(result.Error);

        var enrolments = (result.Data ?? new List<EnrolmentDto>())
            .OrderBy(e => e.Status == EnrolmentStatus.Withdrawn)
            .ThenBy(e => e.EventDate)
            .ToList();
        return View(enrolments);
    }

    [HttpPost]
    public async Task<IActionResult> Pay(PaymentForm form)
    {
        if (!ModelState.IsValid)
        {
            FlashError("Choose a payment method.");
            return RedirectToAction(nameof(MyEnrolments));
        }

        var result = await _api.PayAsync(form);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;

        if (result.Success) FlashSuccess("Payment received. Your place is confirmed!");
        else FlashError(result.Error);
        return RedirectToAction(nameof(MyEnrolments));
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id)
    {
        var result = await _api.WithdrawAsync(id);
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;

        if (result.Success) FlashSuccess("You've withdrawn from the event. Any entry fee you paid has been marked for refund.");
        else FlashError(result.Error);
        return RedirectToAction(nameof(MyEnrolments));
    }

    /// <summary>Personal race history: event, date, category, finish time and placing.</summary>
    public async Task<IActionResult> MyResults()
    {
        var result = await _api.GetMyResultsAsync();
        if (RedirectForAuthFailure(result) is IActionResult redirect) return redirect;
        if (!result.Success) FlashError(result.Error);

        return View(result.Data ?? new List<ResultDto>());
    }
}
