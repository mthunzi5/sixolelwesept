using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Helpers;

namespace RaceDay.Tests;

/// <summary>User registration and login.</summary>
public class AuthControllerTests
{
    private static RegisterRequest NewRegistration(UserRole role = UserRole.Participant, string email = "new.user@example.com") => new()
    {
        FullName = "New User",
        Email = email,
        Password = "Password123!",
        Role = role,
        PhoneNumber = "0820000000"
    };

    [Theory]
    [InlineData(UserRole.Organiser)]
    [InlineData(UserRole.Participant)]
    public async Task Register_WithValidDetails_Returns201AndSavesUserWithChosenRole(UserRole role)
    {
        using var db = TestData.NewDb();
        var controller = TestData.Controller<AuthController>(db);

        var result = await controller.Register(NewRegistration(role));

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var dto = Assert.IsType<UserDto>(created.Value);
        Assert.Equal(role, dto.Role);
        Assert.Equal("new.user@example.com", dto.Email);

        var saved = Assert.Single(db.Users);
        Assert.Equal(role, saved.Role);
    }

    [Fact]
    public async Task Register_HashesPassword_AndNeverStoresPlainText()
    {
        using var db = TestData.NewDb();
        var controller = TestData.Controller<AuthController>(db);

        await controller.Register(NewRegistration());

        var saved = Assert.Single(db.Users);
        Assert.NotEqual("Password123!", saved.PasswordHash);
        Assert.StartsWith("$2", saved.PasswordHash); // BCrypt hash prefix
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", saved.PasswordHash));
    }

    [Fact]
    public async Task Register_WithEmailAlreadyInUse_Returns409()
    {
        using var db = TestData.NewDb();
        TestData.AddUser(db, UserRole.Participant, "taken@example.com");
        var controller = TestData.Controller<AuthController>(db);

        // Different casing/whitespace must still be detected as the same email.
        var result = await controller.Register(NewRegistration(email: "  TAKEN@example.com "));

        var conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task Register_WithUndefinedRole_Returns400()
    {
        using var db = TestData.NewDb();
        var controller = TestData.Controller<AuthController>(db);
        var request = NewRegistration();
        request.Role = (UserRole)99;

        var result = await controller.Register(request);

        var bad = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200AndStoresUserAndRoleInSession()
    {
        using var db = TestData.NewDb();
        var organiser = TestData.AddUser(db, UserRole.Organiser, "org@example.com");
        var controller = TestData.Controller<AuthController>(db);

        var result = await controller.Login(new LoginRequest { Email = "org@example.com", Password = TestData.Password });

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal(organiser.UserId, body.User.UserId);

        var session = controller.HttpContext.Session;
        Assert.Equal(organiser.UserId, session.GetInt32(SessionKeys.UserId));
        Assert.Equal("Organiser", session.GetString(SessionKeys.Role));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401AndDoesNotStartSession()
    {
        using var db = TestData.NewDb();
        TestData.AddUser(db, UserRole.Participant, "p@example.com");
        var controller = TestData.Controller<AuthController>(db);

        var result = await controller.Login(new LoginRequest { Email = "p@example.com", Password = "WrongPassword!" });

        var unauthorized = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.Null(controller.HttpContext.Session.GetInt32(SessionKeys.UserId));
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        using var db = TestData.NewDb();
        var controller = TestData.Controller<AuthController>(db);

        var result = await controller.Login(new LoginRequest { Email = "nobody@example.com", Password = TestData.Password });

        var unauthorized = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public void Logout_ClearsSession()
    {
        using var db = TestData.NewDb();
        var user = TestData.AddUser(db, UserRole.Participant);
        var controller = TestData.Controller<AuthController>(db, loggedInAs: user);

        var result = controller.Logout();

        Assert.IsType<NoContentResult>(result);
        Assert.Null(controller.HttpContext.Session.GetInt32(SessionKeys.UserId));
    }
}

/// <summary>The same flows over real HTTP, proving the session cookie carries the login across requests.</summary>
public class AuthEndpointTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public AuthEndpointTests(RaceDayApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterThenLogin_SessionCookieAuthenticatesLaterRequests()
    {
        var client = _factory.CreateClient();
        var email = $"flow-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Flow Tester", email, password = "Password123!", role = "Participant"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        // Registering alone does not log you in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".RaceDay.Session=") && c.Contains("httponly"));

        var me = await client.GetFromJsonAsync<UserDto>("/api/users/me", TestJson.Options);
        Assert.Equal(email, me!.Email);
        Assert.Equal(UserRole.Participant, me.Role);
    }

    [Fact]
    public async Task Register_WithInvalidRoleOrMissingFields_Returns400()
    {
        var client = _factory.CreateClient();

        var badRole = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Bad Role", email = "badrole@example.com", password = "Password123!", role = "Admin"
        });
        var missingFields = await client.PostAsJsonAsync("/api/auth/register", new { email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, badRole.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingFields.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession_SoProtectedEndpointsReturn401()
    {
        var (client, _) = await _factory.LoggedInClientAsync(UserRole.Organiser);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }
}
