using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Helpers;

namespace RaceDay.Tests;

/// <summary>Event management: Organisers can create, update and delete their own events, and anyone can view events.</summary>
public class EventsControllerTests
{
    private static EventRequest NewEventRequest(string name = "Soweto 10km Challenge") => new()
    {
        EventName = name,
        Description = "Flat and fast.",
        EventDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(2)),
        Location = "Soweto, Gauteng",
        DistanceKm = 10,
        EventType = EventType.Run
    };

    [Fact]
    public async Task CreateEvent_AsOrganiser_Returns201AndSavesEventOwnedByOrganiser()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var controller = TestData.Controller<EventsController>(db, organiser);

        var result = await controller.CreateEvent(NewEventRequest());

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<EventDto>(created.Value);
        Assert.Equal(organiser.UserId, dto.OrganiserId);
        Assert.Equal(EventStatus.Scheduled, dto.Status);

        var saved = Assert.Single(db.Events);
        Assert.Equal("Soweto 10km Challenge", saved.EventName);
        Assert.Equal(EventType.Run, saved.EventType);
        Assert.Equal(10, saved.DistanceKm);
        Assert.Equal("Soweto, Gauteng", saved.Location);
        Assert.Equal(organiser.UserId, saved.OrganiserId);
    }

    [Fact]
    public async Task CreateEvent_WithPastDate_Returns400()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var controller = TestData.Controller<EventsController>(db, organiser);
        var request = NewEventRequest();
        request.EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));

        var result = await controller.CreateEvent(request);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(db.Events);
    }

    [Fact]
    public async Task UpdateEvent_AsOwner_Returns200AndSavesChanges()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, organiser);
        var controller = TestData.Controller<EventsController>(db, organiser);

        var request = NewEventRequest("Renamed Cycle Tour");
        request.EventType = EventType.Cycle;
        request.DistanceKm = 94.7m;
        request.Status = EventStatus.Ongoing;

        var result = await controller.UpdateEvent(ev.EventId, request);

        var dto = Assert.IsType<EventDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("Renamed Cycle Tour", dto.EventName);

        var saved = db.Events.Single();
        Assert.Equal(EventType.Cycle, saved.EventType);
        Assert.Equal(94.7m, saved.DistanceKm);
        Assert.Equal(EventStatus.Ongoing, saved.Status);
    }

    [Fact]
    public async Task UpdateEvent_ByDifferentOrganiser_Returns403AndLeavesEventUnchanged()
    {
        using var db = TestData.NewDb();
        var owner = TestData.AddUser(db, UserRole.Organiser);
        var otherOrganiser = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, owner);
        var originalName = ev.EventName;
        var controller = TestData.Controller<EventsController>(db, otherOrganiser);

        var result = await controller.UpdateEvent(ev.EventId, NewEventRequest("Hijacked"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(originalName, db.Events.Single().EventName);
    }

    [Fact]
    public async Task UpdateEvent_ThatDoesNotExist_Returns404()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var controller = TestData.Controller<EventsController>(db, organiser);

        var result = await controller.UpdateEvent(999, NewEventRequest());

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsOwnerWithNoEnrolments_Returns204AndRemovesEventAndCategories()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, organiser);
        TestData.AddCategory(db, ev, "5km");
        TestData.AddCategory(db, ev, "10km");
        var controller = TestData.Controller<EventsController>(db, organiser);

        var result = await controller.DeleteEvent(ev.EventId);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.Events);
        Assert.Empty(db.Categories);
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_Returns409AndKeepsEvent()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var participant = TestData.AddUser(db, UserRole.Participant);
        var ev = TestData.AddEvent(db, organiser);
        TestData.AddEnrolment(db, participant, TestData.AddCategory(db, ev));
        var controller = TestData.Controller<EventsController>(db, organiser);

        var result = await controller.DeleteEvent(ev.EventId);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Single(db.Events);
    }

    [Fact]
    public async Task DeleteEvent_ByDifferentOrganiser_Returns403()
    {
        using var db = TestData.NewDb();
        var owner = TestData.AddUser(db, UserRole.Organiser);
        var otherOrganiser = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, owner);
        var controller = TestData.Controller<EventsController>(db, otherOrganiser);

        var result = await controller.DeleteEvent(ev.EventId);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Single(db.Events);
    }

    [Fact]
    public async Task GetMyEvents_ReturnsOnlyTheLoggedInOrganisersEvents()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var otherOrganiser = TestData.AddUser(db, UserRole.Organiser);
        TestData.AddEvent(db, organiser);
        TestData.AddEvent(db, organiser);
        TestData.AddEvent(db, otherOrganiser);
        var controller = TestData.Controller<EventsController>(db, organiser);

        var result = await controller.GetMyEvents();

        var events = Assert.IsAssignableFrom<IEnumerable<EventDto>>(Assert.IsType<OkObjectResult>(result).Value).ToList();
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(organiser.UserId, e.OrganiserId));
    }

    [Fact]
    public async Task GetEvents_FiltersByEventType()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        TestData.AddEvent(db, organiser);
        var cycle = TestData.AddEvent(db, organiser);
        cycle.EventType = EventType.Cycle;
        db.SaveChanges();
        var controller = TestData.Controller<EventsController>(db);

        var result = await controller.GetEvents(status: null, eventType: EventType.Cycle, from: null, to: null);

        var events = Assert.IsAssignableFrom<IEnumerable<EventDto>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(cycle.EventId, Assert.Single(events).EventId);
    }

    [Fact]
    public async Task GetEvent_ReturnsEventWithItsCategories()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, organiser);
        TestData.AddCategory(db, ev, "Under 20");
        TestData.AddCategory(db, ev, "Senior");
        var controller = TestData.Controller<EventsController>(db);

        var result = await controller.GetEvent(ev.EventId);

        var dto = Assert.IsType<EventDetailDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(2, dto.Categories.Count);
    }
}
