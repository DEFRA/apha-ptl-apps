using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the Scheme Razor views (Index/Details/History/_SchemeForm)
// actually render at least once, rather than only being exercised via controller unit tests that
// never invoke the view engine - mirrors CustomerRouteSmokeTests.
public class SchemeRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeSchemeApiClient _fakeApiClient;

    public SchemeRouteSmokeTests(WebApplicationFactory<Program> factory)
    {
        var schemeId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var scheme = SampleScheme(schemeId, sharedId);

        _fakeApiClient = new FakeSchemeApiClient
        {
            SchemeResponse = scheme,
            SearchResponse = new SchemeSearchResponse([new SchemeSummaryResponse(sharedId, scheme.YearId, schemeId, scheme.Identifier, scheme.Name, null, null, null, null, null, null)], 1, 1, 20),
            HistoryResponse = [new SchemeHistoryResponse(schemeId, sharedId, scheme.YearId, scheme.Identifier, scheme.Name)]
        };

        SchemeId = schemeId;
        SharedId = sharedId;
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
            }));
    }

    private Guid SchemeId { get; }
    private Guid SharedId { get; }

    private static SchemeResponse SampleScheme(Guid schemeId, Guid sharedId, bool isReadOnly = false) => new(
        schemeId, sharedId, DateTime.UtcNow.Year + 1, "PT1234", "Test Scheme", Guid.NewGuid(), Guid.NewGuid(), null,
        true, false, false, false, false, false, false, false, false, false, false, false, false,
        0, Guid.NewGuid(), 5, 12345, "UK", 10, string.Empty, false, null, null, string.Empty,
        false, false, false, false, false, false, false,
        "Biological samples", false, null, "Instructions", false, false, false,
        null, null, null, null, false, false, null, null, null, null, null,
        DateTime.UtcNow, isReadOnly);

    [Theory]
    [InlineData("/Scheme/Index")]
    [InlineData("/Scheme/Create")]
    public async Task StaticRoutes_ReturnSuccess(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/Details/{SchemeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_RendersFullBreadcrumbTrail()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/Details/{SchemeId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Manage Schemes", body);
        Assert.Contains("Scheme Details", body);
    }

    [Fact]
    public async Task Edit_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/Edit/{SchemeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Edit_RendersFullBreadcrumbTrail()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/Edit/{SchemeId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Manage Schemes", body);
        Assert.Contains("Edit Scheme", body);
    }

    [Fact]
    public async Task History_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/History?sharedId={SharedId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_NoResults_RendersEmptyState()
    {
        _fakeApiClient.SearchResponse = new SchemeSearchResponse([], 0, 1, 20);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Scheme/Index?yearId=2027&searchTerm=nothing-matches");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("There are no schemes to display", body);
    }
}
