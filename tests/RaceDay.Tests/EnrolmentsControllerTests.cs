using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Helpers;

namespace RaceDay.Tests;

/// <summary>Event enrolments: Participants enrol by choosing a category, and the link is recorded correctly.</summary>
public class EnrolmentsControllerTests
{
    [Fact]
    public async Task Enrol_AsParticipant_Returns201AndRecordsParticipantCategoryAndEvent()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var participant = TestData.AddUser(db, UserRole.Participant);
        var ev = TestData.AddEvent(db, organiser);
        var category = TestData.AddCategory(db, ev, "21km");
        var controller = TestData.Controller<EnrolmentsController>(db, participant);

        var result = await controller.Enrol(category.CategoryId);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<EnrolmentDto>(created.Value);
        Assert.Equal(participant.UserId, dto.ParticipantId);
        Assert.Equal(category.CategoryId, dto.CategoryId);
        Assert.Equal(ev.EventId, dto.EventId);
        Assert.False(string.IsNullOrWhiteSpace(dto.BibNumber));

        // The link between participant, category and event is what's actually stored.
        var saved = Assert.Single(db.Enrolments);
        Assert.Equal(participant.UserId, saved.ParticipantId);
        Assert.Equal(category.CategoryId, saved.CategoryId);
        Assert.Equal(ev.EventId, db.Categories.Single(c => c.CategoryId == saved.CategoryId).EventId);
        Assert.Equal(dto.BibNumber, saved.BibNumber);
    }

    [Fact]
    public async Task Enrol_InPaidCategory_IsPendingUntilPaid_FreeCategoryIsConfirmed()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, organiser);
        var paid = TestData.AddCategory(db, ev, "Paid", entryFee: 200);
        var free = TestData.AddCategory(db, TestData.AddEvent(db, organiser), "Free", entryFee: 0);
        var participant = TestData.AddUser(db, UserRole.Participant);
        var controller = TestData.Controller<EnrolmentsController>(db, participant);

        await controller.Enrol(paid.CategoryId);
        await controller.Enrol(free.CategoryId);

        Assert.Equal(EnrolmentStatus.Pending, db.Enrolments.Single(e => e.CategoryId == paid.CategoryId).Status);
        Assert.Equal(EnrolmentStatus.Confirmed, db.Enrolments.Single(e => e.CategoryId == free.CategoryId).Status);
    }

    [Fact]
    public async Task Enrol_EachEntrantGetsAUniqueBibNumber()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var category = TestData.AddCategory(db, TestData.AddEvent(db, organiser));

        for (var i = 0; i < 3; i++)
        {
            var participant = TestData.AddUser(db, UserRole.Participant);
            await TestData.Controller<EnrolmentsController>(db, participant).Enrol(category.CategoryId);
        }

        var bibs = db.Enrolments.Select(e => e.BibNumber).ToList();
        Assert.Equal(3, bibs.Count);
        Assert.Equal(3, bibs.Distinct().Count());
    }

    [Fact]
    public async Task Enrol_TwiceInSameCategory_Returns409AndKeepsOneEnrolment()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var participant = TestData.AddUser(db, UserRole.Participant);
        var category = TestData.AddCategory(db, TestData.AddEvent(db, organiser));
        var controller = TestData.Controller<EnrolmentsController>(db, participant);

        await controller.Enrol(category.CategoryId);
        var second = await controller.Enrol(category.CategoryId);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(second).StatusCode);
        Assert.Single(db.Enrolments);
    }

    [Fact]
    public async Task Enrol_InSecondCategoryOfSameEvent_Returns409()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var participant = TestData.AddUser(db, UserRole.Participant);
        var ev = TestData.AddEvent(db, organiser);
        var tenK = TestData.AddCategory(db, ev, "10km");
        var halfMarathon = TestData.AddCategory(db, ev, "21km");
        var controller = TestData.Controller<EnrolmentsController>(db, participant);

        await controller.Enrol(tenK.CategoryId);
        var result = await controller.Enrol(halfMarathon.CategoryId);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Single(db.Enrolments);
    }

    [Fact]
    public async Task Enrol_WhenCategoryIsFull_Returns409()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var category = TestData.AddCategory(db, TestData.AddEvent(db, organiser), maxParticipants: 1);
        TestData.AddEnrolment(db, TestData.AddUser(db, UserRole.Participant), category);
        var latecomer = TestData.AddUser(db, UserRole.Participant);

        var result = await TestData.Controller<EnrolmentsController>(db, latecomer).Enrol(category.CategoryId);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Single(db.Enrolments);
    }

    [Fact]
    public async Task Enrol_InEventThatIsNotScheduled_Returns409()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var participant = TestData.AddUser(db, UserRole.Participant);
        var category = TestData.AddCategory(db, TestData.AddEvent(db, organiser, status: EventStatus.Cancelled));

        var result = await TestData.Controller<EnrolmentsController>(db, participant).Enrol(category.CategoryId);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(db.Enrolments);
    }

    [Fact]
    public async Task Enrol_InCategoryThatDoesNotExist_Returns404()
    {
        using var db = TestData.NewDb();
        var participant = TestData.AddUser(db, UserRole.Participant);

        var result = await TestData.Controller<EnrolmentsController>(db, participant).Enrol(12345);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Withdraw_ThenReEnrol_ReactivatesOriginalEnrolmentAndBib()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var participant = TestData.AddUser(db, UserRole.Participant);
        var category = TestData.AddCategory(db, TestData.AddEvent(db, organiser));
        var controller = TestData.Controller<EnrolmentsController>(db, participant);

        await controller.Enrol(category.CategoryId);
        var original = db.Enrolments.Single();
        var originalBib = original.BibNumber;

        Assert.IsType<NoContentResult>(await controller.Withdraw(original.EnrolmentId));
        Assert.Equal(EnrolmentStatus.Withdrawn, db.Enrolments.Single().Status);

        Assert.IsType<CreatedAtActionResult>(await controller.Enrol(category.CategoryId));
        var reactivated = Assert.Single(db.Enrolments);
        Assert.NotEqual(EnrolmentStatus.Withdrawn, reactivated.Status);
        Assert.Equal(originalBib, reactivated.BibNumber);
    }

    [Fact]
    public async Task GetMyEnrolments_ReturnsOnlyTheLoggedInParticipantsEnrolments()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var me = TestData.AddUser(db, UserRole.Participant);
        var someoneElse = TestData.AddUser(db, UserRole.Participant);
        var ev = TestData.AddEvent(db, organiser);
        var category = TestData.AddCategory(db, ev);
        TestData.AddEnrolment(db, me, category);
        TestData.AddEnrolment(db, someoneElse, category);

        var result = await TestData.Controller<EnrolmentsController>(db, me).GetMyEnrolments();

        var list = Assert.IsAssignableFrom<IEnumerable<EnrolmentDto>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(me.UserId, Assert.Single(list).ParticipantId);
    }

    [Fact]
    public async Task OrganiserCanViewAllEnrolmentsForTheirEvent_ButNotForAnotherOrganisersEvent()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var otherOrganiser = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, organiser);
        var fiveK = TestData.AddCategory(db, ev, "5km");
        var tenK = TestData.AddCategory(db, ev, "10km");
        TestData.AddEnrolment(db, TestData.AddUser(db, UserRole.Participant), fiveK);
        TestData.AddEnrolment(db, TestData.AddUser(db, UserRole.Participant), tenK);

        var ownResult = await TestData.Controller<EventsController>(db, organiser).GetEventEnrolments(ev.EventId);
        var othersResult = await TestData.Controller<EventsController>(db, otherOrganiser).GetEventEnrolments(ev.EventId);

        var list = Assert.IsAssignableFrom<IEnumerable<EnrolmentDto>>(Assert.IsType<OkObjectResult>(ownResult).Value);
        Assert.Equal(2, list.Count());
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(othersResult).StatusCode);
    }

    [Fact]
    public async Task GetEnrolment_ByAnotherParticipant_Returns403()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var owner = TestData.AddUser(db, UserRole.Participant);
        var stranger = TestData.AddUser(db, UserRole.Participant);
        var enrolment = TestData.AddEnrolment(db, owner, TestData.AddCategory(db, TestData.AddEvent(db, organiser)));

        var result = await TestData.Controller<EnrolmentsController>(db, stranger).GetEnrolment(enrolment.EnrolmentId);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
    }
}

/// <summary>End-to-end enrolment flow over HTTP, from login through enrolment to the organiser's roster.</summary>
public class EnrolmentEndpointTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EnrolmentEndpointTests(RaceDayApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ParticipantEnrols_AndOrganiserSeesThemOnTheCategoryRoster()
    {
        var (organiserClient, organiser) = await _factory.LoggedInClientAsync(UserRole.Organiser);
        var (participantClient, participant) = await _factory.LoggedInClientAsync(UserRole.Participant);
        var categoryId = _factory.WithDb(db => TestData.AddCategory(db, TestData.AddEvent(db, organiser)).CategoryId);

        var enrol = await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);
        Assert.Equal(HttpStatusCode.Created, enrol.StatusCode);
        var enrolment = await enrol.Content.ReadFromJsonAsync<EnrolmentDto>(TestJson.Options);
        Assert.Equal(participant.UserId, enrolment!.ParticipantId);
        Assert.Equal(categoryId, enrolment.CategoryId);

        var roster = await organiserClient.GetFromJsonAsync<List<EnrolmentDto>>($"/api/categories/{categoryId}/enrolments", TestJson.Options);
        Assert.Equal(participant.UserId, Assert.Single(roster!).ParticipantId);

        var mine = await participantClient.GetFromJsonAsync<List<EnrolmentDto>>("/api/participants/me/enrolments", TestJson.Options);
        Assert.Contains(mine!, e => e.EnrolmentId == enrolment.EnrolmentId);
    }
}
