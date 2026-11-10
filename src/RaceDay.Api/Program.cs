using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Swagger;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb"),
        // Retry transient connection failures, e.g. while a SQL Server container is still starting.
        sql => sql.EnableRetryOnFailure(maxRetryCount: 10, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

// Azure Blob Storage for event banners and profile pictures (see Services/AzureBlobImageStorage.cs).
builder.Services.AddSingleton<IBlobImageStorage, AzureBlobImageStorage>();

// Session-based authentication: login stores the user's ID and role in a server-side
// session, and the browser holds only an opaque, HttpOnly session cookie.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        // Enums travel as their names ("Organiser", "Run") rather than numbers.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description =
            "RESTful API for the RaceDay event management system.\n\n" +
            "**How to test protected endpoints:** call `POST /api/auth/login`. The response sets a session cookie " +
            "that the browser sends automatically, so every later request in this page runs as that user. " +
            "Call `POST /api/auth/logout` to switch accounts.\n\n" +
            "Seeded accounts (password `Password123!`): Organisers `naledi.khumalo@raceday.co.za`, " +
            "`sipho.dlamini@raceday.co.za`. Participants `amahle.nkosi@example.com`, `jason.vanwyk@example.com`, " +
            "`palesa.mokoena@example.com`, `ryan.govender@example.com`."
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
    options.OperationFilter<SessionAuthOperationFilter>();
    options.SupportNonNullableReferenceTypes();
});

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

// Swagger is left on in every environment because it is how the API is demonstrated and tested.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    options.DocumentTitle = "RaceDay API";
    options.DisplayRequestDuration();
});

app.UseHttpsRedirection();
app.UseSession();
app.MapControllers();

app.Run();

// Exposed so the integration tests can spin the API up with WebApplicationFactory.
public partial class Program { }
