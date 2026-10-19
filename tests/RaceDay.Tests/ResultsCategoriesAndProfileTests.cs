using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Helpers;

namespace RaceDay.Tests;

/// <summary>Results: Organisers capture finish times and positions after an event, and Participants view their own.</summary>
public class ResultsControllerTests
{
    private static (Api.Data.RaceDayDbContext Db, User Organiser, User Participant, Enrolment Enrolment) PastEventWithEntrant()
    {
        var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var participant = TestData.AddUser(db, UserRole.Participant);
        var ev = TestData.AddEvent(db, organiser, date: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
        var enrolment = TestData.AddEnrolment(db, participant, TestData.AddCategory(db, ev));
        return (db, organiser, participant, enrolment);
    }

    [Fact]
    public async Task CaptureResult_AsEventOrganiser_Returns201AndSavesTimeAndPosition()
    {
        var (db, organiser, _, enrolment) = PastEventWithEntrant();
        using var _db = db;

        var result = await TestData.Controller<ResultsController>(db, organiser)
            .CaptureResult(enrolment.EnrolmentId, new ResultRequest { FinishTimeSeconds = 3125, Position = 1 });

        var dto = Assert.IsType<ResultDto>(Assert.IsType<CreatedResult>(result).Value);
        Assert.Equal("00:52:05", dto.FinishTime);
        var saved = Assert.Single(db.Results);
        Assert.Equal(3125, saved.FinishTimeSeconds);
        Assert.Equal(1, saved.Position);
        Assert.Equal(enrolment.EnrolmentId, saved.EnrolmentId);
    }

    [Fact]
    public async Task CaptureResult_BeforeEventHasHappened_Returns409()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var participant = TestData.AddUser(db, UserRole.Participant);
        var enrolment = TestData.AddEnrolment(db, participant, TestData.AddCategory(db, TestData.AddEvent(db, organiser)));

        var result = await TestData.Controller<ResultsController>(db, organiser)
            .CaptureResult(enrolment.EnrolmentId, new ResultRequest { FinishTimeSeconds = 3000, Position = 1 });

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(db.Results);
    }

    [Fact]
    public async Task CaptureResult_ByOrganiserOfAnotherEvent_Returns403()
    {
        var (db, _, _, enrolment) = PastEventWithEntrant();
        using var _db = db;
        var otherOrganiser = TestData.AddUser(db, UserRole.Organiser);

        var result = await TestData.Controller<ResultsController>(db, otherOrganiser)
            .CaptureResult(enrolment.EnrolmentId, new ResultRequest { FinishTimeSeconds = 3000, Position = 1 });

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(ResultStatus.Finished, null, 1)]   // finished but no time
    [InlineData(ResultStatus.Finished, 3000, null)] // finished but no position
    [InlineData(ResultStatus.DNF, null, 4)]         // DNF cannot have a position
    public async Task CaptureResult_WithInconsistentData_Returns400(ResultStatus status, int? seconds, int? position)
    {
        var (db, organiser, _, enrolment) = PastEventWithEntrant();
        using var _db = db;

        var result = await TestData.Controller<ResultsController>(db, organiser)
            .CaptureResult(enrolment.EnrolmentId, new ResultRequest { Status = status, FinishTimeSeconds = seconds, Position = position });

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetMyResults_ReturnsOnlyTheParticipantsOwnResults()
    {
        var (db, organiser, participant, enrolment) = PastEventWithEntrant();
        using var _db = db;
        var rival = TestData.AddUser(db, UserRole.Participant);
        var rivalEnrolment = TestData.AddEnrolment(db, rival, db.Categories.Single());
        var results = TestData.Controller<ResultsController>(db, organiser);
        await results.CaptureResult(enrolment.EnrolmentId, new ResultRequest { FinishTimeSeconds = 3000, Position = 2 });
        await results.CaptureResult(rivalEnrolment.EnrolmentId, new ResultRequest { FinishTimeSeconds = 2900, Position = 1 });

        var mine = await TestData.Controller<ResultsController>(db, participant).GetMyResults();

        var list = Assert.IsAssignableFrom<IEnumerable<ResultDto>>(Assert.IsType<OkObjectResult>(mine).Value);
        var only = Assert.Single(list);
        Assert.Equal(participant.UserId, only.ParticipantId);
        Assert.Equal(2, only.Position);
    }
}

/// <summary>Categories: Organisers define age or distance categories for their events.</summary>
public class CategoriesControllerTests
{
    [Fact]
    public async Task CreateAgeCategory_AsEventOwner_Returns201()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, organiser);

        var result = await TestData.Controller<CategoriesController>(db, organiser).CreateCategory(ev.EventId, new CategoryRequest
        {
            CategoryName = "Under 20", CategoryType = CategoryType.Age, MaxAge = 19, DistanceKm = 10, EntryFee = 80, MaxParticipants = 50
        });

        var dto = Assert.IsType<CategoryDto>(Assert.IsType<CreatedAtActionResult>(result).Value);
        Assert.Equal(CategoryType.Age, dto.CategoryType);
        Assert.Equal(ev.EventId, Assert.Single(db.Categories).EventId);
    }

    [Fact]
    public async Task CreateAgeCategory_WithoutAgeBounds_Returns400()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, organiser);

        var result = await TestData.Controller<CategoriesController>(db, organiser).CreateCategory(ev.EventId, new CategoryRequest
        {
            CategoryName = "Senior", CategoryType = CategoryType.Age, DistanceKm = 10, MaxParticipants = 50
        });

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateName_Returns409()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, organiser);
        TestData.AddCategory(db, ev, "10km");

        var result = await TestData.Controller<CategoriesController>(db, organiser).CreateCategory(ev.EventId, new CategoryRequest
        {
            CategoryName = "10km", DistanceKm = 10, MaxParticipants = 50
        });

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task CreateCategory_OnAnotherOrganisersEvent_Returns403()
    {
        using var db = TestData.NewDb();
        var owner = TestData.AddUser(db, UserRole.Organiser);
        var intruder = TestData.AddUser(db, UserRole.Organiser);
        var ev = TestData.AddEvent(db, owner);

        var result = await TestData.Controller<CategoriesController>(db, intruder).CreateCategory(ev.EventId, new CategoryRequest
        {
            CategoryName = "5km", DistanceKm = 5, MaxParticipants = 50
        });

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(db.Categories);
    }
}

/// <summary>User profile: both roles can view and update their own profile.</summary>
public class UsersControllerTests
{
    [Theory]
    [InlineData(UserRole.Organiser)]
    [InlineData(UserRole.Participant)]
    public async Task UpdateMe_ChangesOwnProfile(UserRole role)
    {
        using var db = TestData.NewDb();
        var user = TestData.AddUser(db, role);
        var controller = TestData.Controller<UsersController>(db, user);

        var result = await controller.UpdateMe(new UpdateProfileRequest { FullName = "Updated Name", PhoneNumber = "0711111111" });

        var dto = Assert.IsType<UserDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("Updated Name", dto.FullName);
        Assert.Equal("0711111111", db.Users.Single().PhoneNumber);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns401()
    {
        using var db = TestData.NewDb();
        var user = TestData.AddUser(db, UserRole.Participant);
        var originalHash = user.PasswordHash;

        var result = await TestData.Controller<UsersController>(db, user)
            .ChangePassword(new ChangePasswordRequest { CurrentPassword = "not-it", NewPassword = "NewPassword456!" });

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(originalHash, db.Users.Single().PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_StoresNewHash()
    {
        using var db = TestData.NewDb();
        var user = TestData.AddUser(db, UserRole.Organiser);

        var result = await TestData.Controller<UsersController>(db, user)
            .ChangePassword(new ChangePasswordRequest { CurrentPassword = TestData.Password, NewPassword = "NewPassword456!" });

        Assert.IsType<OkObjectResult>(result);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword456!", db.Users.Single().PasswordHash));
    }
}
