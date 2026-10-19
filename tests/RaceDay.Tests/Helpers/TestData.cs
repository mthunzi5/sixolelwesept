using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Tests.Helpers;

/// <summary>Builds isolated in-memory databases, sample rows and controllers with a fake logged-in session.</summary>
public static class TestData
{
    public const string Password = "Password123!";

    /// <summary>A fresh, empty in-memory database. Each call gets its own, so tests never share state.</summary>
    public static RaceDayDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase($"RaceDayTests-{Guid.NewGuid()}")
            .Options;
        return new RaceDayDbContext(options);
    }

    /// <summary>
    /// Creates a controller whose HttpContext carries a session. Pass a user to simulate being logged in
    /// as them, or null for an anonymous caller.
    /// </summary>
    public static T Controller<T>(RaceDayDbContext db, User? loggedInAs = null) where T : ControllerBase
    {
        var controller = (T)Activator.CreateInstance(typeof(T), db)!;
        var httpContext = new DefaultHttpContext { Session = new TestSession() };

        if (loggedInAs is not null)
        {
            httpContext.Session.SetInt32(SessionKeys.UserId, loggedInAs.UserId);
            httpContext.Session.SetString(SessionKeys.Role, loggedInAs.Role.ToString());
            httpContext.Session.SetString(SessionKeys.FullName, loggedInAs.FullName);
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    public static User AddUser(RaceDayDbContext db, UserRole role, string? email = null)
    {
        var user = new User
        {
            FullName = $"Test {role}",
            Email = email ?? $"{role.ToString().ToLower()}-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4),
            Role = role
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static Event AddEvent(RaceDayDbContext db, User organiser, DateOnly? date = null, EventStatus status = EventStatus.Scheduled)
    {
        var ev = new Event
        {
            OrganiserId = organiser.UserId,
            EventName = $"Test Event {Guid.NewGuid():N}"[..20],
            EventDate = date ?? DateOnly.FromDateTime(DateTime.Today.AddMonths(1)),
            Location = "Johannesburg",
            DistanceKm = 10,
            EventType = EventType.Run,
            Status = status
        };
        db.Events.Add(ev);
        db.SaveChanges();
        return ev;
    }

    public static Category AddCategory(RaceDayDbContext db, Event ev, string name = "10km", int maxParticipants = 100, decimal entryFee = 150)
    {
        var category = new Category
        {
            EventId = ev.EventId,
            CategoryName = name,
            DistanceKm = 10,
            EntryFee = entryFee,
            MaxParticipants = maxParticipants
        };
        db.Categories.Add(category);
        db.SaveChanges();
        return category;
    }

    public static Enrolment AddEnrolment(RaceDayDbContext db, User participant, Category category, EnrolmentStatus status = EnrolmentStatus.Confirmed)
    {
        var enrolment = new Enrolment
        {
            ParticipantId = participant.UserId,
            CategoryId = category.CategoryId,
            BibNumber = $"T-{Guid.NewGuid():N}"[..10],
            Status = status
        };
        db.Enrolments.Add(enrolment);
        db.SaveChanges();
        return enrolment;
    }
}
