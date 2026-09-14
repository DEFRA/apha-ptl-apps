using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PTL.ApiClient.Tests;

public class ApiConnectivityHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenApiReportsHealthy()
    {
        var healthCheck = new ApiConnectivityHealthCheck(
            new FakeApiClient(new ApiHealthResponse("Healthy", 1, DateTime.UtcNow)));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenApiReportsNonHealthyStatus()
    {
        var healthCheck = new ApiConnectivityHealthCheck(
            new FakeApiClient(new ApiHealthResponse("Unhealthy", 1, DateTime.UtcNow)));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenApiIsUnreachable()
    {
        var healthCheck = new ApiConnectivityHealthCheck(
            new FakeApiClient(throwOnGetHealth: new HttpRequestException("connection refused")));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotNull(result.Exception);
    }

    private sealed class FakeApiClient(ApiHealthResponse? response = null, Exception? throwOnGetHealth = null) : IApiClient
    {
        private readonly ApiHealthResponse? _response = response ?? new ApiHealthResponse("Healthy", 1, DateTime.UtcNow);

        public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
            throwOnGetHealth is not null
                ? Task.FromException<ApiHealthResponse?>(throwOnGetHealth)
                : Task.FromResult(_response);
    }
}
