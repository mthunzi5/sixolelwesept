using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));

builder.Services.AddControllers();

var app = builder.Build();

// Apply any pending migrations and load sample data on startup, so a fresh clone only
// needs `dotnet run`. Skipped for the in-memory database the tests use.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
        DbSeeder.Seed(db);
    }
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Exposed so the integration tests can spin the API up with WebApplicationFactory.
public partial class Program { }
