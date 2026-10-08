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
            HistoryResponse = [new SchemeHistoryResponse(schemeId, sharedId, scheme.YearId, scheme.Identifier, scheme.Name)],
            RenewResponse = scheme with { SchemeId = Guid.Empty, YearId = scheme.YearId + 1 }
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
    public async Task Create_RendersTheSevenLegacyTabsInOrder()
    {
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/Scheme/Create");

        var expectedTabs = new[]
        {
            "Details", "Tests", "Distribution Level Data", "Results Tabulations",
            "Test Consultants", "Assessors", "Viewers"
        };

        var position = 0;
        foreach (var tab in expectedTabs)
        {
            var marker = $"govuk-tabs__tab\" href=\"#tab-";
            var index = html.IndexOf($">{tab}</a>", position, StringComparison.Ordinal);
            Assert.True(index > 0, $"Tab '{tab}' was not rendered after the preceding tab. Marker: {marker}");
            position = index;
        }
    }

    [Fact]
    public async Task Create_HidesTheAssessorsTabWhileAssessmentIsNotRequired()
    {
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/Scheme/Create");

        // Requires Assessment defaults to false on a new scheme, so Test Consultants is the
        // visible tab and Assessors is hidden (legacy EnableAssessor).
        Assert.Contains("id=\"tab-list-item-assessors\" hidden", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"tab-list-item-test-consultants\" hidden", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_RendersTheTabulationControlsAndTheTestConsultantSelector()
    {
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/Scheme/Create");

        Assert.Contains("Add New Tabulation", html, StringComparison.Ordinal);
        Assert.Contains("No Tabulations Defined", html, StringComparison.Ordinal);
        Assert.Contains("Standard Tabulation Text", html, StringComparison.Ordinal);
        Assert.Contains("External reference used on Tabulations", html, StringComparison.Ordinal);
        Assert.Contains("Score Samples", html, StringComparison.Ordinal);

        // Assessment is not required by default, so the Test Consultant tabulation selector shows.
        Assert.Contains("Select the Tabulation that will be sent to the Test Consultants", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_RendersStaffingSelectorsAndTheViewerTransferLists()
    {
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/Scheme/Create");

        Assert.Contains("Primary Test Consultant", html, StringComparison.Ordinal);
        Assert.Contains("Deputy Test Consultant", html, StringComparison.Ordinal);
        Assert.Contains("Secondary Test Consultant", html, StringComparison.Ordinal);
        Assert.Contains("Primary Assessor", html, StringComparison.Ordinal);
        Assert.Contains("Quaternary Assessor", html, StringComparison.Ordinal);
        Assert.Contains("id=\"availableViewers\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"schemeViewers\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintableSchemes_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Scheme/PrintableSchemes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PrintableScheme_ReturnsSuccessAndRendersLogoAndPrintButton()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/PrintableScheme/{SchemeId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("vetqasheader-greyscale.png", html, StringComparison.Ordinal);
        Assert.Contains("window.print()", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Details_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/Details/{SchemeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Renew_RendersTheCreateFormWithoutSaveAffordancesMissing()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Scheme/Renew/{SchemeId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("type=\"submit\"", html, StringComparison.Ordinal);
        Assert.Contains($"value=\"{SharedId}\"", html, StringComparison.OrdinalIgnoreCase);
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
    public async Task Details_RendersTheSevenLegacyTabsAsReadOnlyContent()
    {
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync($"/Scheme/Details/{SchemeId}");

        var expectedTabs = new[]
        {
            "Details", "Tests", "Distribution Level Data", "Results Tabulations",
            "Test Consultants", "Viewers"
        };

        var position = 0;
        foreach (var tab in expectedTabs)
        {
            var index = html.IndexOf($">{tab}</a>", position, StringComparison.Ordinal);
            Assert.True(index > 0, $"Tab '{tab}' was not rendered after the preceding tab.");
            position = index;
        }

        // Details tab fields render as plain summary-list text in view mode.
        Assert.Contains("govuk-summary-list__key\">Identifier</dt>", html, StringComparison.Ordinal);
        Assert.Contains("PT1234", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Details_DoesNotRenderEditOrSaveAffordances()
    {
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync($"/Scheme/Details/{SchemeId}");

        Assert.DoesNotContain("type=\"submit\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Save<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"testCommand\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"NewTabulationName\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"govuk-input\" id=\"Identifier\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ptl-rich-text", html, StringComparison.Ordinal);
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

    [Fact]
    public async Task ManageSchemes_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Scheme/ManageSchemes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ManageSchemes_RendersHeading()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Scheme/ManageSchemes");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-heading", body, StringComparison.OrdinalIgnoreCase);
    }
}
