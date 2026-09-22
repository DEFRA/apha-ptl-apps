using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PTL.Common.Health;

/// <summary>
/// Shared health-endpoint wiring for every PTL service (PTL.Api, PTL.InternalWeb,
/// PTL.ExternalWeb, ...) - identical for each, so it lives here rather than being
/// duplicated per app.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Liveness only - process is responsive, no dependency calls, cheap.
        // This is what an ALB target-group/ECS container health check
        // should point at; safe to leave reachable since it reveals
        // nothing and can't be abused to generate load.
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

        // Checks whatever this app's registered IHealthCheck(s) report (e.g. DB
        // connectivity in PTL.Api, Api connectivity in PTL.InternalWeb/ExternalWeb).
        // Gated behind ReadinessKeyFilter and deliberately separate from the
        // liveness endpoint above - never point an ALB/ECS health check at this.
        // For on-demand/manual diagnostics and internal monitoring only.
        app.MapGroup("/health/ready")
            .AddEndpointFilter<ReadinessKeyFilter>()
            .MapHealthChecks("", new HealthCheckOptions { ResponseWriter = HealthCheckResponseWriter.WriteResponse });
    }
}
