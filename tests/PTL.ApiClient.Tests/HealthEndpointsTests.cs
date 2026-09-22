using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PTL.Common.Health;

namespace PTL.ApiClient.Tests;

public class HealthEndpointsTests
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ReturnsNotFound_WhenKeyMissing()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ReturnsOk_WhenKeyCorrect()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(ReadinessKeyFilter.HeaderName, "test-key");

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HealthCheck:ReadinessKey"] = "test-key"
        });
        builder.Services.AddHealthChecks()
            .AddCheck("dummy", () => HealthCheckResult.Healthy());

        var app = builder.Build();
        app.MapHealthEndpoints();
        await app.StartAsync();
        return app;
    }
}
