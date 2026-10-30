using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Full-pipeline smoke tests for story 744650 (Results -> View Results): the Previous Distributions
// search/grid screen (legacy DistributionsPrevious.aspx) and the read-only Results screen (legacy
// ResultsView.aspx).
public class DistributionPreviousAndResultsRouteSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public DistributionPreviousAndResultsRouteSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    private static ParticipantPastDistributionResponse Distribution(
        string reference, string schemeCode, string schemeName, DateTime date, int yearId, int monthId,
        Guid? monthlyDistributionSchemeId = null) =>
        new(
            monthlyDistributionSchemeId ?? Guid.NewGuid(),
            reference,
            schemeCode,
            schemeName,
            date,
            yearId,
            monthId);

    private HttpClient CreateClient(FakeDistributionApiClient apiClient) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(apiClient);
            })).CreateClient();

    private static async Task<Dictionary<string, string>> SignInAsync(HttpClient client, string roles = "Participant")
    {
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync($"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString(roles)}&participantId={Guid.NewGuid()}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);
        return cookies;
    }

    [Fact]
    public async Task Previous_ParticipantWithDistributions_RendersGridWithViewLinks()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ParticipantHistory = new ParticipantPastDistributionListResponse(
                [Distribution("D1a/00001", "PT1234", "Test Scheme", new DateTime(2026, 1, 15), 2025, 1)])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Previous", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("D1a/00001", body);
        Assert.Contains("PT1234", body);
        Assert.Contains("Test Scheme", body);
        Assert.Contains("/Distribution/Results", body);
    }

    [Fact]
    public async Task Previous_NoDistributions_RendersEmptyStateMessage()
    {
        var client = CreateClient(new FakeDistributionApiClient());
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Previous", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("There are no distributions to display.", body);
    }

    [Fact]
    public async Task Previous_SearchCriteriaNarrowsResults()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ParticipantHistory = new ParticipantPastDistributionListResponse(
            [
                Distribution("D1a/00001", "PT1234", "Match Scheme", new DateTime(2026, 1, 15), 2025, 1),
                Distribution("D1a/00002", "PT5678", "Other Scheme", new DateTime(2026, 2, 15), 2025, 2),
            ])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, "/Distribution/Previous?schemeCode=PT1234", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("D1a/00001", body);
        Assert.DoesNotContain("D1a/00002", body);
    }

    [Fact]
    public async Task Previous_SearchWithNoMatches_RendersNoResultsMessage()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ParticipantHistory = new ParticipantPastDistributionListResponse(
                [Distribution("D1a/00001", "PT1234", "Test Scheme", new DateTime(2026, 1, 15), 2025, 1)])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, "/Distribution/Previous?schemeCode=NoMatch", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("No results found for the selected search criteria.", body);
    }

    [Fact]
    public async Task Previous_ColumnHeaderSort_TogglesDirection()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ParticipantHistory = new ParticipantPastDistributionListResponse(
            [
                Distribution("D1a/00002", "PT5678", "Scheme B", new DateTime(2026, 2, 1), 2025, 2),
                Distribution("D1a/00001", "PT1234", "Scheme A", new DateTime(2026, 1, 1), 2025, 1),
            ])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, "/Distribution/Previous?sortBy=reference&sortDir=asc", cookies);
        var body = await response.Content.ReadAsStringAsync();

        var firstIndex = body.IndexOf("D1a/00001", StringComparison.Ordinal);
        var secondIndex = body.IndexOf("D1a/00002", StringComparison.Ordinal);
        Assert.True(firstIndex >= 0 && firstIndex < secondIndex);
    }

    [Fact]
    public async Task Previous_MoreDistributionsThanPageSize_RendersPaginationNav()
    {
        var distributions = Enumerable.Range(1, 20)
            .Select(i => Distribution($"D1a/{i:D5}", "PT1234", $"Scheme {i}", new DateTime(2026, 1, 1).AddDays(i), 2025, 1))
            .ToList();
        var apiClient = new FakeDistributionApiClient { ParticipantHistory = new ParticipantPastDistributionListResponse(distributions) };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Previous", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("govuk-pagination", body);
    }

    [Fact]
    public async Task Results_ParticipantOwnsDistribution_RendersReadOnlyWithNoFormControls()
    {
        var monthlyDistributionSchemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = new ActualResultsResponse(
                Guid.NewGuid(),
                monthlyDistributionSchemeId,
                "D1a/00001 Test Scheme",
                "D1a/00001 Test Scheme",
                new DateTime(2026, 1, 1),
                false,
                [new ActualResultsTestResponse(
                    Guid.NewGuid(),
                    "Culture test",
                    [new ActualResultsResultItemResponse(Guid.NewGuid(), "Growth", "Positive|Negative", "Text")],
                    [],
                    [new ActualResultsSampleRowResponse(Guid.NewGuid(), "S001", [])])])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, $"/Distribution/Results?monthlyDistributionSchemeId={monthlyDistributionSchemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Culture test", body);

        // Scoped to <main> - the shared layout's header legitimately has its own sign-out <form>/<button>.
        var main = Regex.Match(body, "<main.*?</main>", RegexOptions.Singleline).Value;
        Assert.DoesNotContain("<form", main, StringComparison.Ordinal);
        Assert.DoesNotContain("<input", main, StringComparison.Ordinal);
        Assert.DoesNotContain("<select", main, StringComparison.Ordinal);
        Assert.DoesNotContain("<button", main, StringComparison.Ordinal);
        Assert.Matches("<a[^>]*href=\"/Distribution/Previous\"[^>]*>Back</a>", main);
    }

    [Fact]
    public async Task Results_ApiResolvesNoResultsForThisParticipant_RendersNotFound()
    {
        // Covers both "no results exist for this distribution" and the Access Restrictions AC: the
        // API only ever resolves a distributionParticipantId that belongs to the calling
        // participantId (see DistributionRepository.ResolveDistributionParticipantIdAsync's
        // fldParticipantId = @ParticipantId filter), so a tampered monthlyDistributionSchemeId that
        // belongs to someone else also resolves to nothing, exactly like this case.
        var apiClient = new FakeDistributionApiClient { ActualResults = null };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, $"/Distribution/Results?monthlyDistributionSchemeId={Guid.NewGuid()}", cookies);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Previous_ViewerOnlyUser_IsForbidden()
    {
        var client = CreateClient(new FakeDistributionApiClient());
        var cookies = await SignInAsync(client, roles: "Viewer");

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Previous", cookies);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HomeQuickLinkCard_ViewResults_NavigatesToRealDistributionControllerNotComingSoon()
    {
        var client = CreateClient(new FakeDistributionApiClient());
        var cookies = await SignInAsync(client);

        var homeResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        var homeBody = await homeResponse.Content.ReadAsStringAsync();

        var hrefMatch = Regex.Match(homeBody, "href=\"(?<href>[^\"]*Distribution[^\"]*title=View(%20|\\+)results[^\"]*)\"");
        Assert.True(hrefMatch.Success, "Expected to find a real href to DistributionController for the 'View results' quick-link card.");
        Assert.DoesNotContain("ComingSoon", hrefMatch.Groups["href"].Value);
    }
}
