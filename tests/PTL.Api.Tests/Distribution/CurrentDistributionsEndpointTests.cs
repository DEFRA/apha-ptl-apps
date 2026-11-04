using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// The external Current Distributions list is served by its own controller; these tests go through the real
// routing so a moved or clashing route is caught (the controller unit tests call the method directly).
public class CurrentDistributionsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CurrentDistributionsEndpointTests(WebApplicationFactory<Program> factory) =>
        // Every background flag off so no test run reaches a real database (see TabulationEmailRegistrationTests).
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TabulationEmails:Enabled"] = "false",
                ["PublicationNotifications:Enabled"] = "false",
                ["OnlineOrderEmails:Enabled"] = "false",
                ["CustomerUpdateEmails:Enabled"] = "false",
                ["ParticipantUpdateEmails:Enabled"] = "false",
            })));

    private sealed class FakeCurrentDistributionsService(IReadOnlyList<CurrentDistributionData> rows) : ICurrentDistributionsService
    {
        public List<Guid> RequestedParticipantIds { get; } = [];

        public Task<IReadOnlyList<CurrentDistributionData>> GetCurrentDistributionsAsync(Guid participantId, CancellationToken cancellationToken = default)
        {
            RequestedParticipantIds.Add(participantId);
            return Task.FromResult(rows);
        }
    }

    private HttpClient CreateClient(FakeCurrentDistributionsService service) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICurrentDistributionsService>();
            services.AddSingleton<ICurrentDistributionsService>(service);
        })).CreateClient();

    [Fact]
    public async Task GetCurrent_ThroughTheRealRouting_ReturnsTheParticipantsRows()
    {
        var participantId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var distributionParticipantId = Guid.NewGuid();
        var service = new FakeCurrentDistributionsService(
        [
            new CurrentDistributionData(
                schemeId, distributionParticipantId, "D26-01A/BA", "PT001", "Bacteriology",
                new DateTime(2026, 4, 3), new DateTime(2026, 4, 30), HasResults: true, Submitted: false)
        ]);
        var client = CreateClient(service);

        var response = await client.GetAsync($"/api/distributions/participants/{participantId}/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<CurrentDistributionResponse>>();
        var row = Assert.Single(rows!);
        Assert.Equal((schemeId, distributionParticipantId, "D26-01A/BA", "PT001", "Bacteriology"),
            (row.MonthlyDistributionSchemeId, row.DistributionParticipantId, row.DistributionReference, row.SchemeCode, row.SchemeName));
        Assert.Equal((new DateTime(2026, 4, 3), new DateTime(2026, 4, 30), true, false),
            (row.DistributionDate, row.DeadlineDate, row.HasResults, row.Submitted));
        Assert.Equal(participantId, Assert.Single(service.RequestedParticipantIds));
    }

    [Fact]
    public async Task GetCurrent_NoRows_ReturnsAnEmptyList()
    {
        var client = CreateClient(new FakeCurrentDistributionsService([]));

        var response = await client.GetAsync($"/api/distributions/participants/{Guid.NewGuid()}/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<CurrentDistributionResponse>>())!);
    }

    [Fact]
    public async Task GetCurrent_ParticipantIdThatIsNotAGuid_IsNotFound()
    {
        var client = CreateClient(new FakeCurrentDistributionsService([]));

        var response = await client.GetAsync("/api/distributions/participants/not-a-guid/current");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
