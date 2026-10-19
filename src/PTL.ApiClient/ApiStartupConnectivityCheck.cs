using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PTL.ApiClient;

/// <summary>
/// Calls PTL.Api's liveness endpoint once, in the background, after the web app starts and logs the
/// outcome. A broken network path to PTL.Api (security group, DNS/Service Connect, routing) otherwise
/// only surfaces on the first user action that needs the Api, and a silent packet drop looks like a
/// plain timeout. This never blocks startup or fails the host: the web app still serves everything
/// that doesn't depend on the Api, so the result is only logged.
/// </summary>
public sealed partial class ApiStartupConnectivityCheck(
    IApiClient apiClient,
    IConfiguration configuration,
    ILogger<ApiStartupConnectivityCheck> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseUrl = configuration["Api:BaseUrl"];

        try
        {
            var health = await apiClient.GetHealthAsync(stoppingToken);
            if (health?.Status == "Healthy")
            {
                LogApiReachable(baseUrl);
            }
            else
            {
                LogApiUnhealthy(baseUrl, health?.Status);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down before the check finished - nothing to report.
        }
        catch (Exception exception)
        {
            LogApiUnreachable(baseUrl, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Startup check: PTL.Api is reachable at {ApiBaseUrl}")]
    private partial void LogApiReachable(string? apiBaseUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup check: PTL.Api at {ApiBaseUrl} responded but did not report Healthy (status: {Status})")]
    private partial void LogApiUnhealthy(string? apiBaseUrl, string? status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Startup check: PTL.Api is NOT reachable at {ApiBaseUrl}. Check DNS/Service Connect, security groups (inbound to the Api port) and routing; sign-in and any Api-backed feature will fail until this is fixed")]
    private partial void LogApiUnreachable(string? apiBaseUrl, Exception exception);
}
