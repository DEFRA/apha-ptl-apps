using System.Net;
using System.Net.Http.Json;
using PTL.ApiClient;
using PTL.Common.Health;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

public class HealthEndpointsTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public HealthEndpointsTests(PtlExternalWebTestFactory factory)
    {
        _factory = factory;
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

    private sealed class HealthResponse
    {
        public string? Status { get; set; }
    }
}
