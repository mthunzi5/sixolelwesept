using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using RaceDay.Api.Auth;
using RaceDay.Api.Models;
using RaceDay.Tests.Helpers;

namespace RaceDay.Tests;

/// <summary>Unit tests for the [SessionAuthorize] filter that guards every non-public endpoint.</summary>
public class SessionAuthorizeAttributeTests
{
    private static AuthorizationFilterContext ContextFor(UserRole? loggedInRole)
    {
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        if (loggedInRole is UserRole role)
        {
            httpContext.Session.SetInt32(SessionKeys.UserId, 1);
            httpContext.Session.SetString(SessionKeys.Role, role.ToString());
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static int? StatusOf(AuthorizationFilterContext context) => (context.Result as ObjectResult)?.StatusCode;

    [Fact]
    public void NoSession_Returns401()
    {
        var context = ContextFor(null);

        new SessionAuthorizeAttribute(UserRole.Organiser).OnAuthorization(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusOf(context));
    }

    [Fact]
    public void ParticipantOnOrganiserEndpoint_Returns403()
    {
        var context = ContextFor(UserRole.Participant);

        new SessionAuthorizeAttribute(UserRole.Organiser).OnAuthorization(context);

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(context));
    }

    [Fact]
    public void OrganiserOnParticipantEndpoint_Returns403()
    {
        var context = ContextFor(UserRole.Organiser);

        new SessionAuthorizeAttribute(UserRole.Participant).OnAuthorization(context);

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(context));
    }

    [Theory]
    [InlineData(UserRole.Organiser, UserRole.Organiser)]
    [InlineData(UserRole.Participant, UserRole.Participant)]
    public void MatchingRole_IsAllowedThrough(UserRole loggedIn, UserRole required)
    {
        var context = ContextFor(loggedIn);

        new SessionAuthorizeAttribute(required).OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData(UserRole.Organiser)]
    [InlineData(UserRole.Participant)]
    public void NoRoleRequired_AllowsAnyLoggedInUser(UserRole loggedIn)
    {
        var context = ContextFor(loggedIn);

        new SessionAuthorizeAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }
}

/// <summary>
/// Calls every role-restricted endpoint over HTTP with the wrong role (and with no login) and checks it is
/// rejected before any data changes. IDs in the URLs don't need to exist because the role check runs first.
/// </summary>
public class RoleAccessEndpointTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public RoleAccessEndpointTests(RaceDayApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string> OrganiserOnlyEndpoints => new()
    {
        { "GET", "/api/organisers/me/events" },
        { "POST", "/api/events" },
        { "PUT", "/api/events/1" },
        { "DELETE", "/api/events/1" },
        { "GET", "/api/events/1/enrolments" },
        { "POST", "/api/events/1/categories" },
        { "PUT", "/api/categories/1" },
        { "DELETE", "/api/categories/1" },
        { "GET", "/api/categories/1/enrolments" },
        { "POST", "/api/enrolments/1/result" },
        { "PUT", "/api/results/1" },
    };

    public static TheoryData<string, string> ParticipantOnlyEndpoints => new()
    {
        { "POST", "/api/categories/1/enrolments" },
        { "GET", "/api/participants/me/enrolments" },
        { "GET", "/api/participants/me/results" },
        { "POST", "/api/enrolments/1/payment" },
    };

    public static TheoryData<string, string> AnyLoggedInEndpoints => new()
    {
        { "GET", "/api/users/me" },
        { "PUT", "/api/users/me" },
        { "PUT", "/api/users/me/password" },
        { "GET", "/api/users/1" },
        { "GET", "/api/enrolments/1" },
        { "DELETE", "/api/enrolments/1" },
        { "GET", "/api/enrolments/1/payment" },
        { "GET", "/api/auth/session" },
    };

    private static HttpRequestMessage Request(string method, string url) =>
        new(new HttpMethod(method), url) { Content = JsonContent.Create(new { }) };

    [Theory]
    [MemberData(nameof(OrganiserOnlyEndpoints))]
    public async Task Participant_IsForbiddenFromOrganiserEndpoints(string method, string url)
    {
        var (client, _) = await _factory.LoggedInClientAsync(UserRole.Participant);

        var response = await client.SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ParticipantOnlyEndpoints))]
    public async Task Organiser_IsForbiddenFromParticipantEndpoints(string method, string url)
    {
        var (client, _) = await _factory.LoggedInClientAsync(UserRole.Organiser);

        var response = await client.SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(OrganiserOnlyEndpoints))]
    [MemberData(nameof(ParticipantOnlyEndpoints))]
    [MemberData(nameof(AnyLoggedInEndpoints))]
    public async Task AnonymousCaller_GetsUnauthorizedOnEveryProtectedEndpoint(string method, string url)
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/events/1/categories")]
    [InlineData("/api/events/1/results")]
    public async Task PublicEndpoints_DoNotRequireLogin(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_AndNothingIsSaved()
    {
        var (client, _) = await _factory.LoggedInClientAsync(UserRole.Participant);
        var name = $"Sneaky {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/events", new
        {
            eventName = name, eventDate = DateTime.Today.AddMonths(1).ToString("yyyy-MM-dd"),
            location = "Durban", distanceKm = 5, eventType = "Walk"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(_factory.WithDb(db => db.Events.Any(e => e.EventName == name)));
    }

    [Fact]
    public async Task Organiser_CannotEnrolInAnEvent_AndNothingIsSaved()
    {
        var (client, organiser) = await _factory.LoggedInClientAsync(UserRole.Organiser);
        var categoryId = _factory.WithDb(db => TestData.AddCategory(db, TestData.AddEvent(db, organiser)).CategoryId);

        var response = await client.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(_factory.WithDb(db => db.Enrolments.Any(en => en.CategoryId == categoryId)));
    }
}
