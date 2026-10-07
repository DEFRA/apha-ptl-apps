using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PTL.Common.Health;

namespace PTL.Api.Tests.Endpoints;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ReadinessKey = "local-dev-readiness-key";

    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        // The API's startup checks (StartupChecks.RequireDatabaseOptions/RequireReadinessKey) run
        // as top-level statements in Program.cs, reading builder.Configuration before the host is
        // built - too early for WithWebHostBuilder's ConfigureAppConfiguration hook, which only
        // takes effect once the deferred host is built. Environment variables, however, are
        // already picked up by WebApplication.CreateBuilder itself, so they're set here instead.
        // Normally these come from appsettings.Development.json locally or ECS-injected
        // Database__*/HealthCheck__ReadinessKey variables in a deployed environment, neither of
        // which exists in a fresh CI checkout.
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "ProficiencyTesting");
        Environment.SetEnvironmentVariable("Database__IntegratedSecurity", "true");
        Environment.SetEnvironmentVariable("Database__TrustServerCertificate", "true");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", ReadinessKey);

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
    public async Task HealthReady_ReturnsNotFound_WhenReadinessKeyNotConfigured()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HealthCheck:ReadinessKey"] = ""
            })));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ReadinessKeyFilter.HeaderName, ReadinessKey);

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
        client.DefaultRequestHeaders.Add(ReadinessKeyFilter.HeaderName, ReadinessKey);

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
