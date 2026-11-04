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

    private HttpClient CreateClient(FakeDistributionApiClient apiClient, bool allowAutoRedirect = true) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(apiClient);
            })).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });

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
    public async Task Previous_RendersLegacyTitleIntroAndYearDropdownOfHistoryYears()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ParticipantHistory = new ParticipantPastDistributionListResponse(
            [
                Distribution("D1a/00001", "PT1234", "Scheme A", new DateTime(2026, 1, 15), 2025, 1),
                Distribution("D1a/00002", "PT5678", "Scheme B", new DateTime(2025, 6, 15), 2025, 6),
            ])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Previous?year=2026", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(">Previous distributions</h1>", body, StringComparison.Ordinal);
        Assert.Contains("Please select the month and year of the distribution, the distribution reference number or the scheme name of the distribution you wish to view.", body, StringComparison.Ordinal);
        var year = Regex.Match(body, "<select[^>]*id=\"year\".*?</select>", RegexOptions.Singleline).Value;
        Assert.DoesNotContain("type=\"number\"", body, StringComparison.Ordinal);
        List<string> expectedOptions = ["All", "2025", "2026"];
        Assert.Equal(expectedOptions, Regex.Matches(year, "<option[^>]*>([^<]*)</option>").Select(m => m.Groups[1].Value).ToList());
        Assert.Matches("<option[^>]*value=\"2026\"[^>]*selected", year);
    }

    [Fact]
    public async Task Previous_YearFilter_MatchesYearIdOrDistributionCalendarYear()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ParticipantHistory = new ParticipantPastDistributionListResponse(
            [
                Distribution("D1a/00001", "PT1234", "Scheme A", new DateTime(2026, 1, 15), 2025, 1),
                Distribution("D1a/00002", "PT5678", "Scheme B", new DateTime(2024, 6, 15), 2024, 6),
            ])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var byCalendarYear = await (await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Previous?year=2026", cookies)).Content.ReadAsStringAsync();
        var byYearId = await (await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Previous?year=2025", cookies)).Content.ReadAsStringAsync();

        Assert.Contains("D1a/00001", byCalendarYear, StringComparison.Ordinal);
        Assert.DoesNotContain("D1a/00002", byCalendarYear, StringComparison.Ordinal);
        Assert.Contains("D1a/00001", byYearId, StringComparison.Ordinal);
        Assert.DoesNotContain("D1a/00002", byYearId, StringComparison.Ordinal);
    }

    private const string PreviousUrlWithFilters = "/Distribution/Previous?year=2026&month=1&sortBy=schemeCode&sortDir=desc&pageSize=25";

    [Fact]
    public async Task Previous_ViewLink_CarriesTheCurrentListUrlAsReturnUrl()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ParticipantHistory = new ParticipantPastDistributionListResponse(
                [Distribution("D1a/00001", "PT1234", "Test Scheme", new DateTime(2026, 1, 15), 2025, 1)])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, PreviousUrlWithFilters, cookies);
        var body = (await response.Content.ReadAsStringAsync()).Replace("&amp;", "&", StringComparison.Ordinal);

        Assert.Contains($"returnUrl={Uri.EscapeDataString(PreviousUrlWithFilters)}", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Results_WithReturnUrl_BackReturnsToTheFilteredList()
    {
        var monthlyDistributionSchemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = new ActualResultsResponse(
                Guid.NewGuid(), monthlyDistributionSchemeId, "D1a/00001 Test Scheme", "D1a/00001 Test Scheme", new DateTime(2026, 1, 1), false, [])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client,
            HttpMethod.Get,
            $"/Distribution/Results?monthlyDistributionSchemeId={monthlyDistributionSchemeId}&returnUrl={Uri.EscapeDataString(PreviousUrlWithFilters)}",
            cookies);
        var main = Regex.Match(await response.Content.ReadAsStringAsync(), "<main.*?</main>", RegexOptions.Singleline).Value.Replace("&amp;", "&", StringComparison.Ordinal);

        Assert.Contains($"href=\"{PreviousUrlWithFilters}\">Back</a>", main, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://evil.example/Distribution/Previous")]
    [InlineData("//evil.example/Distribution/Previous")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/Tabulation/Index")]
    public async Task Results_WithUnsafeReturnUrl_IgnoresItAndBacksToThePlainList(string returnUrl)
    {
        var monthlyDistributionSchemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = new ActualResultsResponse(
                Guid.NewGuid(), monthlyDistributionSchemeId, "D1a/00001 Test Scheme", "D1a/00001 Test Scheme", new DateTime(2026, 1, 1), false, [])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client,
            HttpMethod.Get,
            $"/Distribution/Results?monthlyDistributionSchemeId={monthlyDistributionSchemeId}&returnUrl={Uri.EscapeDataString(returnUrl)}",
            cookies);
        var main = Regex.Match(await response.Content.ReadAsStringAsync(), "<main.*?</main>", RegexOptions.Singleline).Value;

        Assert.Matches("<a[^>]*href=\"/Distribution/Previous\"[^>]*>Back</a>", main);
        Assert.DoesNotContain("evil.example", main, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Results_WithReturnUrl_BreadcrumbPreviousDistributionsLinksToTheFilteredList()
    {
        var monthlyDistributionSchemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = new ActualResultsResponse(
                Guid.NewGuid(), monthlyDistributionSchemeId, "D1a/00001 Test Scheme", "D1a/00001 Test Scheme", new DateTime(2026, 1, 1), false, [])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client,
            HttpMethod.Get,
            $"/Distribution/Results?monthlyDistributionSchemeId={monthlyDistributionSchemeId}&returnUrl={Uri.EscapeDataString(PreviousUrlWithFilters)}",
            cookies);
        var breadcrumbs = Regex.Match(await response.Content.ReadAsStringAsync(), "<nav class=\"govuk-breadcrumbs\".*?</nav>", RegexOptions.Singleline).Value
            .Replace("&amp;", "&", StringComparison.Ordinal);

        Assert.Contains($"<a class=\"govuk-breadcrumbs__link\" href=\"{PreviousUrlWithFilters}\">Previous distributions</a>", breadcrumbs, StringComparison.Ordinal);
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

        // Legacy ResultsView: intro text, then one table per sample and a separate method items section.
        Assert.Contains("The tables below show the results that were entered for this distribution.", main, StringComparison.Ordinal);
        Assert.Contains(">Sample S001 results for Culture test</caption>", main, StringComparison.Ordinal);
        Assert.Matches("<th scope=\"row\"[^>]*>Growth</th><td class=\"govuk-table__cell\">", main);
        Assert.Contains("This test has no method items.", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Results_Breadcrumb_ShowsResultsSectionLinkThenCurrentPageAsPlainText()
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
                [])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, $"/Distribution/Results?monthlyDistributionSchemeId={monthlyDistributionSchemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var breadcrumbs = Regex.Match(body, "<nav class=\"govuk-breadcrumbs\".*?</nav>", RegexOptions.Singleline).Value;
        Assert.Contains("<a class=\"govuk-breadcrumbs__link\" href=\"/Distribution/Previous\">Previous distributions</a>", breadcrumbs, StringComparison.Ordinal);
        Assert.Contains("<li class=\"govuk-breadcrumbs__list-item\" aria-current=\"page\">View results</li>", breadcrumbs, StringComparison.Ordinal);
        Assert.DoesNotContain(">Results</", breadcrumbs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_Breadcrumb_ResultsLinksToCurrentDistributionsAndCurrentPageIsPlainText()
    {
        var monthlyDistributionSchemeId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = new ActualResultsResponse(
                participantId,
                monthlyDistributionSchemeId,
                "D1a/00001 Test Scheme",
                "D1a/00001 Test Scheme",
                new DateTime(2026, 1, 1),
                false,
                [])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, $"/Distribution/Entry?schemeId={monthlyDistributionSchemeId}&participantId={participantId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var breadcrumbs = Regex.Match(body, "<nav class=\"govuk-breadcrumbs\".*?</nav>", RegexOptions.Singleline).Value;
        Assert.Contains("<a class=\"govuk-breadcrumbs__link\" href=\"/Distribution/Current\">Current distributions</a>", breadcrumbs, StringComparison.Ordinal);
        Assert.Contains("<li class=\"govuk-breadcrumbs__list-item\" aria-current=\"page\">Results entry</li>", breadcrumbs, StringComparison.Ordinal);
        Assert.DoesNotContain(">Results</", breadcrumbs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_RendersSubmitSaveAndCancel_AndDoesNotPostATrustedParticipantId()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = new ActualResultsResponse(
                Guid.NewGuid(), schemeId, "D1a/00001 Test Scheme", "D1a/00001 Test Scheme", new DateTime(2026, 1, 1), false, [])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, $"/Distribution/Entry?schemeId={schemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var main = Regex.Match(body, "<main.*?</main>", RegexOptions.Singleline).Value;
        Assert.Matches("<button[^>]*value=\"submit\"[^>]*class=\"govuk-button\"[^>]*>Submit</button>", main);
        Assert.Matches("<button[^>]*value=\"save\"[^>]*govuk-button--secondary[^>]*>Save</button>", main);
        Assert.Matches("<button[^>]*value=\"cancel\"[^>]*>Cancel</button>", main);
        Assert.DoesNotContain("DistributionParticipantId", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_RendersLegacyIntroAndOneTablePerSampleWithRequiredFormatOnlyForTextInputs()
    {
        var schemeId = Guid.NewGuid();
        var textItem = Guid.NewGuid();
        var dropdownItem = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = new ActualResultsResponse(
                Guid.NewGuid(), schemeId, "D1a/00001 Test Scheme", "D1a/00001 Test Scheme", new DateTime(2026, 1, 1), false,
                [new ActualResultsTestResponse(
                    Guid.NewGuid(),
                    "ELISA",
                    [
                        new ActualResultsResultItemResponse(textItem, "PI Values", "^.*$", "X(15)"),
                        new ActualResultsResultItemResponse(dropdownItem, "Result", "Positive|Negative", "Text"),
                    ],
                    [new ActualResultsMethodItemResponse(Guid.NewGuid(), "Kit", "^.*$", "X(10)", "")],
                    [
                        new ActualResultsSampleRowResponse(Guid.NewGuid(), "26/7001",
                            [new ActualResultsSampleValueResponse(Guid.NewGuid(), textItem, ""), new ActualResultsSampleValueResponse(Guid.NewGuid(), dropdownItem, "")]),
                        new ActualResultsSampleRowResponse(Guid.NewGuid(), "26/7002",
                            [new ActualResultsSampleValueResponse(Guid.NewGuid(), textItem, ""), new ActualResultsSampleValueResponse(Guid.NewGuid(), dropdownItem, "")]),
                    ])])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, $"/Distribution/Entry?schemeId={schemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var main = Regex.Match(body, "<main.*?</main>", RegexOptions.Singleline).Value;
        Assert.Contains("Enter the Test Results below. Tests are divided into Result Items which are tested for each Sample, and Method Items which apply to the Test itself.", main, StringComparison.Ordinal);
        Assert.Contains("Results can be saved at any time up until the deadline date provided that all the inputs are valid.", main, StringComparison.Ordinal);
        Assert.Contains(">Sample 26/7001</th>", main, StringComparison.Ordinal);
        Assert.Contains(">Sample 26/7002</th>", main, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Count(main, ">Required format</th>"));
        Assert.Equal(2, Regex.Count(main, "<option[^>]*value=\"\"[^>]*>- Please Select -</option>"));
        Assert.Equal(3, Regex.Count(main, "maxlength=\"8000\""));
        Assert.Equal(3, Regex.Count(main, "<td class=\"govuk-table__cell\">X\\((15|10)\\)</td>"));
        Assert.DoesNotContain("Input must be of format", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Current_RendersLegacyStatusTextAndSortableHeaders()
    {
        var apiClient = new FakeDistributionApiClient
        {
            CurrentDistributions =
            [
                new CurrentDistributionResponse(Guid.NewGuid(), Guid.NewGuid(), "D1a/00001", "PT1", "Scheme", new DateTime(2026, 1, 5), new DateTime(2026, 1, 30), HasResults: false, Submitted: false),
                new CurrentDistributionResponse(Guid.NewGuid(), Guid.NewGuid(), "D1a/00002", "PT2", "Scheme", new DateTime(2026, 1, 6), new DateTime(2026, 1, 31), HasResults: true, Submitted: false),
                new CurrentDistributionResponse(Guid.NewGuid(), Guid.NewGuid(), "D1a/00003", "PT3", "Scheme", new DateTime(2026, 1, 7), new DateTime(2026, 2, 1), HasResults: true, Submitted: true),
            ]
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, "/Distribution/Current?sortBy=reference&sortDir=desc", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("In Progress</td>", body, StringComparison.Ordinal);
        Assert.Contains("Submitted</td>", body, StringComparison.Ordinal);
        Assert.Contains("No</td>", body, StringComparison.Ordinal);
        Assert.Contains("aria-sort=\"descending\"", body, StringComparison.Ordinal);
        Assert.Contains("sortBy=status", body, StringComparison.Ordinal);
        Assert.Contains("sortBy=deadlineDate", body, StringComparison.Ordinal);
        Assert.True(body.IndexOf("D1a/00003", StringComparison.Ordinal) < body.IndexOf("D1a/00001", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Previous_RendersTheDistributionDateInTheLegacyDayNameFormat()
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

        Assert.Contains("Thursday 15 January 2026", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Current_MoreThanFifteenDistributions_ShowsFifteenWithPaginationLinksToCurrent()
    {
        var apiClient = new FakeDistributionApiClient
        {
            CurrentDistributions = [.. Enumerable.Range(1, 20).Select(i => new CurrentDistributionResponse(
                Guid.NewGuid(), Guid.NewGuid(), $"D1a/{i:D5}", "PT1", "Scheme", new DateTime(2026, 1, 5), new DateTime(2026, 1, 30), HasResults: false, Submitted: false))]
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Current", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(15, Regex.Count(body, "aria-label=\"View results entry for "));
        Assert.Contains("govuk-pagination", body, StringComparison.Ordinal);
        Assert.Contains("href=\"/Distribution/Current?", body, StringComparison.Ordinal);
        Assert.Contains("page=2", body, StringComparison.Ordinal);
        Assert.Contains("name=\"pageSize\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Current_FifteenOrFewerDistributions_RendersNoPaginationNav()
    {
        var apiClient = new FakeDistributionApiClient
        {
            CurrentDistributions =
            [
                new CurrentDistributionResponse(Guid.NewGuid(), Guid.NewGuid(), "D1a/00001", "PT1", "Scheme", new DateTime(2026, 1, 5), new DateTime(2026, 1, 30), HasResults: false, Submitted: false),
            ]
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Current", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("<nav class=\"govuk-pagination\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Previous_MoreDistributionsThanPageSize_PaginationLinksPointAtPrevious()
    {
        var distributions = Enumerable.Range(1, 20)
            .Select(i => Distribution($"D1a/{i:D5}", "PT1234", $"Scheme {i}", new DateTime(2026, 1, 1).AddDays(i), 2025, 1))
            .ToList();
        var apiClient = new FakeDistributionApiClient { ParticipantHistory = new ParticipantPastDistributionListResponse(distributions) };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Previous", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("href=\"/Distribution/Previous?", body, StringComparison.Ordinal);
        Assert.Contains("page=2", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("schemeCode", "D1a/00002", "D1a/00001")]
    [InlineData("schemeName", "D1a/00002", "D1a/00001")]
    public async Task Previous_SortsByTheSchemeColumns(string sortBy, string firstReference, string secondReference)
    {
        var apiClient = new FakeDistributionApiClient
        {
            ParticipantHistory = new ParticipantPastDistributionListResponse(
            [
                Distribution("D1a/00001", "PT9", "Zulu", new DateTime(2026, 1, 1), 2025, 1),
                Distribution("D1a/00002", "PT1", "Alpha", new DateTime(2026, 2, 1), 2025, 2),
            ])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, $"/Distribution/Previous?sortBy={sortBy}&sortDir=asc", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(body.IndexOf(firstReference, StringComparison.Ordinal) < body.IndexOf(secondReference, StringComparison.Ordinal));
    }

    private sealed record EntryFixture(
        Guid SchemeId, Guid OwnDistributionParticipantId, Guid TestId, Guid ResultItemId, Guid SampleId, Guid SampleValueId, ActualResultsResponse Own);

    private static EntryFixture NewEntryFixture(bool isReadOnly = false)
    {
        var schemeId = Guid.NewGuid();
        var ownId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var resultItemId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var sampleValueId = Guid.NewGuid();
        var own = new ActualResultsResponse(
            ownId, schemeId, "D1a/00001 Test Scheme", "D1a/00001 Test Scheme", new DateTime(2026, 1, 1), false,
            [new ActualResultsTestResponse(
                testId,
                "ELISA",
                [new ActualResultsResultItemResponse(resultItemId, "PI Values", "^[A-Z]+$", "X(15)")],
                [],
                [new ActualResultsSampleRowResponse(sampleId, "26/7001", [new ActualResultsSampleValueResponse(sampleValueId, resultItemId, "")])])],
            isReadOnly);
        return new EntryFixture(schemeId, ownId, testId, resultItemId, sampleId, sampleValueId, own);
    }

    private static Dictionary<string, string> EntryForm(string token, EntryFixture f, string result, string command = "save") => new()
    {
        ["__RequestVerificationToken"] = token,
        ["MonthlyDistributionSchemeId"] = f.SchemeId.ToString(),
        ["Tests[0].TestId"] = f.TestId.ToString(),
        ["Tests[0].ResultItems[0].TestResultItemId"] = f.ResultItemId.ToString(),
        ["Tests[0].Samples[0].SampleId"] = f.SampleId.ToString(),
        ["Tests[0].Samples[0].Values[0].SampleResultItemId"] = f.SampleValueId.ToString(),
        ["Tests[0].Samples[0].Values[0].TestResultItemId"] = f.ResultItemId.ToString(),
        ["Tests[0].Samples[0].Values[0].Result"] = result,
        ["command"] = command,
    };

    private static async Task<(string Token, string Body)> GetEntryTokenAsync(HttpClient client, Dictionary<string, string> cookies, Guid schemeId)
    {
        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, $"/Distribution/Entry?schemeId={schemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();
        return (Regex.Match(body, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value, body);
    }

    [Fact]
    public async Task Entry_Post_RealForm_BindsTheNestedValuesAndSavesAgainstTheOwnRecord()
    {
        var f = NewEntryFixture();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = f.Own,
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>())
        };
        var client = CreateClient(apiClient, allowAutoRedirect: false);
        var cookies = await SignInAsync(client);
        var (token, formBody) = await GetEntryTokenAsync(client, cookies, f.SchemeId);

        // The hand-built field names below must be the ones the real form posts.
        Assert.Contains("name=\"Tests[0].Samples[0].Values[0].Result\"", formBody, StringComparison.Ordinal);
        Assert.Contains("name=\"MonthlyDistributionSchemeId\"", formBody, StringComparison.Ordinal);
        Assert.DoesNotContain("DistributionParticipantId", formBody, StringComparison.Ordinal);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, "/Distribution/Entry", cookies, new FormUrlEncodedContent(EntryForm(token, f, "POSITIVE")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Distribution/Current", response.Headers.Location!.ToString());
        var saved = Assert.Single(apiClient.SavedActualResultsRequests);
        var value = Assert.Single(saved.SampleValues);
        Assert.Equal((f.SampleValueId, "POSITIVE"), (value.SampleResultItemId, value.Result));
        Assert.Equal(f.OwnDistributionParticipantId, Assert.Single(apiClient.SavedActualResultsParticipantIds));
        Assert.False(saved.Submitted);
    }

    [Fact]
    public async Task Entry_Post_SubmitButton_SendsTheSubmittedFlag()
    {
        var f = NewEntryFixture();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = f.Own,
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>())
        };
        var client = CreateClient(apiClient, allowAutoRedirect: false);
        var cookies = await SignInAsync(client);
        var (token, _) = await GetEntryTokenAsync(client, cookies, f.SchemeId);

        await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, "/Distribution/Entry", cookies, new FormUrlEncodedContent(EntryForm(token, f, "POSITIVE", command: "submit")));

        Assert.True(Assert.Single(apiClient.SavedActualResultsRequests).Submitted);
    }

    [Fact]
    public async Task Entry_Post_WithoutTheSchemeId_ReturnsToCurrentAndSavesNothing()
    {
        var f = NewEntryFixture();
        var apiClient = new FakeDistributionApiClient { ActualResults = f.Own };
        var client = CreateClient(apiClient, allowAutoRedirect: false);
        var cookies = await SignInAsync(client);
        var (token, _) = await GetEntryTokenAsync(client, cookies, f.SchemeId);
        var form = EntryForm(token, f, "POSITIVE");
        form.Remove("MonthlyDistributionSchemeId");

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, "/Distribution/Entry", cookies, new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Distribution/Current", response.Headers.Location!.ToString());
        Assert.Empty(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Entry_Post_PastTheDeadline_RendersTheSaveFailedMessageAndSavesNothing()
    {
        var f = NewEntryFixture(isReadOnly: true);
        var apiClient = new FakeDistributionApiClient { ActualResults = f.Own };
        var client = CreateClient(apiClient, allowAutoRedirect: false);
        var cookies = await SignInAsync(client);
        var (token, _) = await GetEntryTokenAsync(client, cookies, f.SchemeId);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, "/Distribution/Entry", cookies, new FormUrlEncodedContent(EntryForm(token, f, "POSITIVE")));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Save Failed: No results can be saved after the deadline date.", body, StringComparison.Ordinal);
        Assert.Contains("value=\"POSITIVE\"", body, StringComparison.Ordinal);
        Assert.Empty(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Entry_Post_ApiReportsAFormatError_RendersItInLegacyWordingOnTheForm()
    {
        var f = NewEntryFixture();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = f.Own,
            ActualResultsSaveResult = new ActualResultsSaveResult(false, new Dictionary<Guid, string> { [f.SampleValueId] = "Input must be of format: X(15)" })
        };
        var client = CreateClient(apiClient, allowAutoRedirect: false);
        var cookies = await SignInAsync(client);
        var (token, _) = await GetEntryTokenAsync(client, cookies, f.SchemeId);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, "/Distribution/Entry", cookies, new FormUrlEncodedContent(EntryForm(token, f, "bad value")));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("PI Values must be in format X(15)", body, StringComparison.Ordinal);
        Assert.Contains("value=\"bad value\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_Post_WithoutAntiforgeryToken_IsRejected()
    {
        var f = NewEntryFixture();
        var apiClient = new FakeDistributionApiClient { ActualResults = f.Own };
        var client = CreateClient(apiClient, allowAutoRedirect: false);
        var cookies = await SignInAsync(client);
        var form = EntryForm("", f, "POSITIVE");
        form.Remove("__RequestVerificationToken");

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, "/Distribution/Entry", cookies, new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Previous_Breadcrumb_ShowsPreviousDistributionsAsPlainCurrentPage()
    {
        var client = CreateClient(new FakeDistributionApiClient());
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Previous", cookies);
        var body = await response.Content.ReadAsStringAsync();

        var breadcrumbs = Regex.Match(body, "<nav class=\"govuk-breadcrumbs\".*?</nav>", RegexOptions.Singleline).Value;
        Assert.Contains("<li class=\"govuk-breadcrumbs__list-item\" aria-current=\"page\">Previous distributions</li>", breadcrumbs, StringComparison.Ordinal);
        Assert.DoesNotContain(">Results</", breadcrumbs, StringComparison.Ordinal);
        Assert.DoesNotContain("View results", breadcrumbs, StringComparison.Ordinal);
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
