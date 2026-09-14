using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace PTL.Api.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Liveness only - process is responsive, no DB dependency, cheap.
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

        // Deliberately separate from the liveness endpoint above, and gated
        // behind ReadinessKeyFilter - never point an ALB/ECS health check at
        // this. Not only would a transient DB blip take an otherwise-healthy
        // container out of rotation, but ALB health checks can't send the
        // required header anyway. For on-demand/manual diagnostics and
        // internal monitoring only.
        app.MapGroup("/health/ready")
            .AddEndpointFilter<ReadinessKeyFilter>()
            .MapHealthChecks("", new HealthCheckOptions { ResponseWriter = HealthCheckResponseWriter.WriteResponse });
    }
}
