var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

// Simple health endpoint for infrastructure readiness/liveness checks
app.MapGet("/health", () =>
{
    var uptimeSeconds = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
    return Results.Json(new
    {
        status = "Healthy",
        uptimeSeconds = Math.Round(uptimeSeconds, 0),
        timestampUtc = DateTime.UtcNow
    });
});

app.Run();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.Api.Tests.
public partial class Program { }
