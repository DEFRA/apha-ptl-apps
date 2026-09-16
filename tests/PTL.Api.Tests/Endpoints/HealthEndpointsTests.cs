using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PTL.Api.Features.Health;

namespace PTL.Api.Tests.Endpoints;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Root_ReturnsHelloWorld()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello World!", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Healthy", body!.Status);
        Assert.True(body.UptimeSeconds >= 0);
    }

    [Fact]
    public async Task HealthReady_ReturnsNotFound_WhenKeyMissing()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ReturnsNotFound_WhenKeyWrong()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ReadinessKeyFilter.HeaderName, "wrong-key");

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ProbesDatabase_WhenKeyCorrect()
    {
        // Deliberately point at an unreachable host (rather than relying on no SQL Server being
        // present in the test environment) so this test is deterministic on any machine,
        // including one with a working local dev database - see DatabaseHealthCheckTests for the
        // health-check logic itself.
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "invalid-host-for-tests,1433",
                ["Database:Name"] = "ProficiencyTesting",
                ["Database:IntegratedSecurity"] = "true",
                ["Database:TrustServerCertificate"] = "true"
            })));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ReadinessKeyFilter.HeaderName, "local-dev-readiness-key");

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed class HealthResponse
    {
        public string? Status { get; set; }
        public double UptimeSeconds { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}
