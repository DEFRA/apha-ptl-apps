using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PTL.ApiClient;

/// <summary>
/// Confirms PTL.Api is reachable. Reports Degraded, not Unhealthy, when it
/// isn't - the calling web app can still serve everything that doesn't
/// depend on Api (static pages, any Api-independent feature), so this must
/// never be treated as the web app itself being down. By default a
/// Degraded result still returns HTTP 200 from the health check
/// middleware, so it never causes an ALB/ECS health check to pull the app
/// out of rotation - it's here purely so a monitoring tool or dashboard
/// reading /health/ready's JSON body can see that the Api dependency is
/// unhealthy.
/// </summary>
public sealed class ApiConnectivityHealthCheck(IApiClient apiClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await apiClient.GetHealthAsync(cancellationToken);
            return health?.Status == "Healthy"
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("PTL.Api responded but did not report Healthy status");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("PTL.Api is unreachable", ex);
        }
    }
}
