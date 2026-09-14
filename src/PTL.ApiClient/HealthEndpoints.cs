using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;

namespace PTL.ApiClient;

/// <summary>
/// Shared health-endpoint wiring for every PTL web front-end (PTL.InternalWeb,
/// PTL.ExternalWeb, ...) - identical for each, so it lives here alongside
/// the typed client rather than being duplicated per app.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Liveness only - process is responsive, no dependency calls, cheap.
        // This is what an ALB target-group/ECS container health check
        // should point at. Deliberately never calls PTL.Api: a slow/failing
        // dependency call here could make this endpoint itself time out,
        // which would get a perfectly healthy web container killed over an
        // Api problem - the opposite of what a liveness check is for.
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

        // Checks whether PTL.Api is reachable (reports Degraded, not
        // Unhealthy - see ApiConnectivityHealthCheck). Gated behind
        // ReadinessKeyFilter and deliberately separate from the liveness
        // endpoint above - never point an ALB/ECS health check at this. For
        // on-demand/manual diagnostics and internal monitoring only.
        app.MapGroup("/health/ready")
            .AddEndpointFilter<ReadinessKeyFilter>()
            .MapHealthChecks("", new HealthCheckOptions { ResponseWriter = HealthCheckResponseWriter.WriteResponse });
    }
}
