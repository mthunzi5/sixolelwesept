using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Tests.Helpers;

/// <summary>
/// Hosts the real API in memory (full middleware pipeline: session cookie, [SessionAuthorize] filter,
/// model validation, JSON) but swaps SQL Server for an in-memory database, so tests run anywhere,
/// including on the GitHub Actions runner.
/// </summary>
public class RaceDayApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"RaceDayApi-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RaceDayDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RaceDayDbContext>>();
            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <summary>Runs an action against the API's database, e.g. to seed rows or check what an endpoint saved.</summary>
    public T WithDb<T>(Func<RaceDayDbContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<RaceDayDbContext>());
    }

    /// <summary>Creates a user directly in the database and returns an HttpClient already logged in as them.</summary>
    public async Task<(HttpClient Client, User User)> LoggedInClientAsync(UserRole role)
    {
        var user = WithDb(db => TestData.AddUser(db, role));
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = TestData.Password });
        response.EnsureSuccessStatusCode();
        return (client, user);
    }
}
