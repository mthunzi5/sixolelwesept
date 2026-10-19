var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Exposed so the integration tests can spin the API up with WebApplicationFactory.
public partial class Program { }
