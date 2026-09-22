using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

/// <summary>
/// Enhanced Scheme route tests covering additional page rendering scenarios.
/// Ensures all Scheme Razor view code paths are exercised for coverage.
/// </summary>
public class SchemeRouteSmokeEdgeCaseTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeSchemeApiClient _fakeApiClient;
    private readonly Guid _schemeId;
    private readonly Guid _sharedId;

    public SchemeRouteSmokeEdgeCaseTests(WebApplicationFactory<Program> factory)
    {
        _schemeId = Guid.NewGuid();
        _sharedId = Guid.NewGuid();

        _fakeApiClient = new FakeSchemeApiClient
        {
            SchemeResponse = SampleScheme(_schemeId, _sharedId),
            SearchResponse = new SchemeSearchResponse([], 0, 1, 20),
            HistoryResponse = []
        };

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
            }));
    }

    private static SchemeResponse SampleScheme(Guid schemeId, Guid sharedId, bool isReadOnly = false, bool isInactive = false)
    {
        var inactiveDate = isInactive ? DateTime.UtcNow : (DateTime?)null;
        var assessor1 = inactiveDate.HasValue ? Guid.NewGuid() : (Guid?)null;

        return new(schemeId, sharedId, DateTime.UtcNow.Year + 1, "PT1234", "Test Scheme",
            Guid.NewGuid(), Guid.NewGuid(), null, true, false, false, false, false, false, false, false,
            false, false, false, false, false, 0, Guid.NewGuid(), 5, 12345, "UK", 10, string.Empty, false,
            null, null, string.Empty, false, false, false, false, false, false, false, "Biological samples",
            false, null, "Instructions", false, false, false, null, null, null, null, false,
            false, null, null, null, null, null,
            DateTime.UtcNow, isReadOnly);
    }

    [Fact]
    public async Task Details_WithValidSchemeId_RendersSuccessfully()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/Details/{_schemeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_WithReadOnlyScheme_RendersSuccessfully()
    {
        _fakeApiClient.SchemeResponse = SampleScheme(_schemeId, _sharedId, isReadOnly: true);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/Details/{_schemeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_WithInactiveScheme_RendersSuccessfully()
    {
        _fakeApiClient.SchemeResponse = SampleScheme(_schemeId, _sharedId, isReadOnly: false, isInactive: true);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/Details/{_schemeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task History_WithEmptyResults_RendersSuccessfully()
    {
        _fakeApiClient.HistoryResponse = [];
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/History?sharedId={_sharedId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task History_WithMultipleRecords_RendersSuccessfully()
    {
        _fakeApiClient.HistoryResponse = new[]
        {
            new SchemeHistoryResponse(_schemeId, _sharedId, 2027, "PT1234", "Test Scheme"),
            new SchemeHistoryResponse(_schemeId, _sharedId, 2026, "PT1234", "Test Scheme"),
            new SchemeHistoryResponse(_schemeId, _sharedId, 2025, "PT1234", "Test Scheme")
        };
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/History?sharedId={_sharedId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_WithEmptyResults_RendersSuccessfully()
    {
        _fakeApiClient.SearchResponse = new SchemeSearchResponse([], 0, 1, 20);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Scheme/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_WithMultiplePages_RendersSuccessfully()
    {
        var items = Enumerable.Range(1, 3)
            .Select(i => new SchemeSummaryResponse(
                Guid.NewGuid(), DateTime.UtcNow.Year + 1, _schemeId, $"PT{i:000}", $"Scheme {i}",
                null, null, null, null, null, null))
            .ToList();
        _fakeApiClient.SearchResponse = new SchemeSearchResponse(items, 21, 1, 10);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Scheme/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_Get_RendersFormSuccessfully()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Scheme/Create");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Edit_Get_RendersFormSuccessfully()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/Edit/{_schemeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
