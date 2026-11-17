using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Scheme;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Full-pipeline smoke tests for the Scheme List screen (legacy SchemeList.aspx) - proves the
// Razor view actually renders, not just that the controller action returns a view result.
public class SchemeRouteSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public SchemeRouteSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Index_ParticipantWithSchemes_RendersCommercialAndNonCommercialTables()
    {
        var participantId = Guid.NewGuid();
        var fakeApiClient = new FakeSchemeApiClient
        {
            ParticipantSchemes = new ParticipantSchemeListResponse(
                [new ParticipantSchemeSummaryResponse(Guid.NewGuid(), "PT1234", "Commercial Test Scheme", "2025/26", true)],
                [new ParticipantSchemeSummaryResponse(Guid.NewGuid(), "PT5678", "Non-Commercial Test Scheme", "2025/26", true)])
        };

        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(fakeApiClient);
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={participantId}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Commercial Test Scheme", body);
        Assert.Contains("PT1234", body);
        Assert.Contains("Non-Commercial Test Scheme", body);
        Assert.Contains("PT5678", body);
        Assert.Contains("2025/26", body);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(body, ">Current contract year<"));
    }

    [Fact]
    public async Task Index_ParticipantWithNoSchemes_RendersEmptyMessages()
    {
        var fakeApiClient = new FakeSchemeApiClient { ParticipantSchemes = new ParticipantSchemeListResponse([], []) };

        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(fakeApiClient);
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={Guid.NewGuid()}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No commercial schemes are available for the current year.", body);
        Assert.Contains("You are not currently registered to any non-commercial schemes.", body);
    }

    [Fact]
    public async Task HomeQuickLinkCard_ViewSchemes_NavigatesToRealSchemeControllerNotComingSoon()
    {
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(new FakeSchemeApiClient());
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var homeResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        var homeBody = await homeResponse.Content.ReadAsStringAsync();

        var hrefMatch = Regex.Match(homeBody, "href=\"(?<href>[^\"]*Scheme[^\"]*title=View(%20|\\+)schemes[^\"]*)\"");
        Assert.True(hrefMatch.Success, "Expected to find a real href to SchemeController for the 'View schemes' quick-link card.");
        Assert.DoesNotContain("ComingSoon", hrefMatch.Groups["href"].Value);
    }

    [Fact]
    public async Task Index_MoreCommercialSchemesThanPageSize_RendersFirstPageAndPaginationNav()
    {
        var commercialSchemes = Enumerable.Range(1, 20)
            .Select(i => new ParticipantSchemeSummaryResponse(Guid.NewGuid(), $"PT{i:D4}", $"Scheme {i}", "2025/26", false))
            .ToList();
        var fakeApiClient = new FakeSchemeApiClient
        {
            ParticipantSchemes = new ParticipantSchemeListResponse(commercialSchemes, [])
        };

        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(fakeApiClient);
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={Guid.NewGuid()}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Default page size (15) - first 15 of 20 schemes shown, the last 5 on page 2.
        Assert.Contains("PT0001", body);
        Assert.Contains("PT0015", body);
        Assert.DoesNotContain("PT0016", body);
        Assert.Contains("govuk-pagination", body);
        Assert.Contains("Items per page", body);
    }

    [Fact]
    public async Task Index_SecondPageRequested_RendersRemainingSchemes()
    {
        var commercialSchemes = Enumerable.Range(1, 20)
            .Select(i => new ParticipantSchemeSummaryResponse(Guid.NewGuid(), $"PT{i:D4}", $"Scheme {i}", "2025/26", false))
            .ToList();
        var fakeApiClient = new FakeSchemeApiClient
        {
            ParticipantSchemes = new ParticipantSchemeListResponse(commercialSchemes, [])
        };

        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(fakeApiClient);
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={Guid.NewGuid()}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index?page=2", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("PT0001<", body);
        Assert.Contains("PT0016", body);
        Assert.Contains("PT0020", body);
    }

    [Fact]
    public async Task Index_PageSizeChanged_RendersRequestedRowCount()
    {
        var commercialSchemes = Enumerable.Range(1, 20)
            .Select(i => new ParticipantSchemeSummaryResponse(Guid.NewGuid(), $"PT{i:D4}", $"Scheme {i}", "2025/26", false))
            .ToList();
        var fakeApiClient = new FakeSchemeApiClient
        {
            ParticipantSchemes = new ParticipantSchemeListResponse(commercialSchemes, [])
        };

        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(fakeApiClient);
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={Guid.NewGuid()}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        // All 20 schemes fit on a single page of 25 - no pagination nav should be rendered.
        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index?pageSize=25", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("PT0001", body);
        Assert.Contains("PT0020", body);
        Assert.DoesNotContain("govuk-pagination", body);
    }

    [Fact]
    public async Task Index_CommercialSchemeIsParticipating_RendersBoldHighlightAndAsteriskMarker()
    {
        var fakeApiClient = new FakeSchemeApiClient
        {
            ParticipantSchemes = new ParticipantSchemeListResponse(
                [
                    new ParticipantSchemeSummaryResponse(Guid.NewGuid(), "PT0001", "Enrolled Scheme", "2025/26", true),
                    new ParticipantSchemeSummaryResponse(Guid.NewGuid(), "PT0002", "Not Enrolled Scheme", "2025/26", false)
                ],
                [])
        };

        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(fakeApiClient);
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={Guid.NewGuid()}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("app-table__row--enrolled", body);
        Assert.Contains("govuk-!-font-weight-bold", body);
        Assert.Contains("You are enrolled in this scheme", body);
        // The non-enrolled row must not pick up the highlight/bold treatment.
        var notEnrolledRowIndex = body.IndexOf("Not Enrolled Scheme", StringComparison.Ordinal);
        var precedingRowStart = body.LastIndexOf("<tr", notEnrolledRowIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("app-table__row--enrolled", body[precedingRowStart..notEnrolledRowIndex]);
    }

    [Fact]
    public async Task Index_NonCommercialSchemeIsParticipating_RendersBoldHighlightAndAsteriskMarker()
    {
        var fakeApiClient = new FakeSchemeApiClient
        {
            ParticipantSchemes = new ParticipantSchemeListResponse(
                [],
                [new ParticipantSchemeSummaryResponse(Guid.NewGuid(), "PT0009", "Non-Commercial Enrolled", "2025/26", true)])
        };

        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(fakeApiClient);
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={Guid.NewGuid()}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("app-table__row--enrolled", body);
        Assert.Contains("govuk-!-font-weight-bold", body);
        Assert.Contains("<span aria-hidden=\"true\"> *</span>", body);
        Assert.Contains("<td class=\"govuk-table__cell govuk-!-font-weight-bold\">2025/26</td>", body);
        Assert.Contains("You are enrolled in this scheme", body);
    }

    private async Task<string> GetSchemeIndexAsync(ParticipantSchemeListResponse schemes, string query = "")
    {
        var fakeApiClient = new FakeSchemeApiClient { ParticipantSchemes = schemes };

        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(fakeApiClient);
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={Guid.NewGuid()}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, $"/Scheme/Index{query}", cookies);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static List<ParticipantSchemeSummaryResponse> NonCommercial(int count) => Enumerable.Range(1, count)
        .Select(i => new ParticipantSchemeSummaryResponse(Guid.NewGuid(), $"NC{i:D4}", $"Non-commercial {i}", "2025/26", true))
        .ToList();

    [Fact]
    public async Task Index_NonCommercialSchemesOverOnePage_PagesTheNonCommercialTableAtFifteenByDefault()
    {
        var body = await GetSchemeIndexAsync(new ParticipantSchemeListResponse([], NonCommercial(20)));

        Assert.Contains("NC0001", body);
        Assert.Contains("NC0015", body);
        Assert.DoesNotContain("NC0016", body);
        Assert.Contains("id=\"ncPageSize\"", body);
        Assert.Contains("ncPage=2", body);
    }

    [Fact]
    public async Task Index_NonCommercialSecondPageRequested_RendersTheRemainingNonCommercialSchemes()
    {
        var body = await GetSchemeIndexAsync(new ParticipantSchemeListResponse([], NonCommercial(20)), "?ncPage=2");

        Assert.DoesNotContain("NC0001<", body);
        Assert.Contains("NC0016", body);
        Assert.Contains("NC0020", body);
    }

    [Fact]
    public async Task Index_NonCommercialPageSizeChanged_RendersTheRequestedRowCount()
    {
        var body = await GetSchemeIndexAsync(new ParticipantSchemeListResponse([], NonCommercial(20)), "?ncPageSize=25");

        Assert.Contains("NC0001", body);
        Assert.Contains("NC0020", body);
        Assert.DoesNotContain("govuk-pagination", body);
    }

    [Fact]
    public async Task Index_FifteenOrFewerNonCommercialSchemes_ShowsNeitherDropdownNorPager()
    {
        var body = await GetSchemeIndexAsync(new ParticipantSchemeListResponse([], NonCommercial(15)));

        Assert.Contains("NC0015", body);
        Assert.DoesNotContain("id=\"ncPageSize\"", body);
        Assert.DoesNotContain("govuk-pagination", body);
    }

    [Fact]
    public async Task Index_ChangingOneTable_KeepsTheOtherTablesPageAndPageSize()
    {
        var commercial = Enumerable.Range(1, 20)
            .Select(i => new ParticipantSchemeSummaryResponse(Guid.NewGuid(), $"PT{i:D4}", $"Scheme {i}", "2025/26", false))
            .ToList();

        var body = await GetSchemeIndexAsync(
            new ParticipantSchemeListResponse(commercial, NonCommercial(20)), "?page=2&ncPage=2&ncPageSize=10");

        // Commercial page 2 of 15 shows PT0016+, non-commercial page 2 of 10 shows NC0011+.
        Assert.Contains("PT0016", body);
        Assert.Contains("NC0011", body);
        Assert.DoesNotContain("NC0010<", body);
        // The commercial dropdown carries the non-commercial state forward as hidden fields.
        Assert.Contains("name=\"ncPage\" value=\"2\"", body);
        Assert.Contains("name=\"ncPageSize\" value=\"10\"", body);
        // The non-commercial dropdown carries the commercial state forward.
        Assert.Contains("name=\"page\" value=\"2\"", body);
    }

    [Fact]
    public async Task Index_CommercialSortByNameDescending_RendersSchemesInDescendingOrder()
    {
        var fakeApiClient = new FakeSchemeApiClient
        {
            ParticipantSchemes = new ParticipantSchemeListResponse(
                [
                    new ParticipantSchemeSummaryResponse(Guid.NewGuid(), "PT0001", "Alpha Scheme", "2025/26", false),
                    new ParticipantSchemeSummaryResponse(Guid.NewGuid(), "PT0002", "Zeta Scheme", "2025/26", false)
                ],
                [])
        };

        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(fakeApiClient);
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={Guid.NewGuid()}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index?sortBy=name&sortDir=desc", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.IndexOf("Zeta Scheme", StringComparison.Ordinal) < body.IndexOf("Alpha Scheme", StringComparison.Ordinal));
        Assert.Contains("sorted descending", body);
    }

    [Fact]
    public async Task Index_NonCommercialSortByYearAscending_RendersSchemesInAscendingOrder()
    {
        var fakeApiClient = new FakeSchemeApiClient
        {
            ParticipantSchemes = new ParticipantSchemeListResponse(
                [],
                [
                    new ParticipantSchemeSummaryResponse(Guid.NewGuid(), "PT0001", "Later Year Scheme", "2026/27", true),
                    new ParticipantSchemeSummaryResponse(Guid.NewGuid(), "PT0002", "Earlier Year Scheme", "2024/25", true)
                ])
        };

        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(fakeApiClient);
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={Guid.NewGuid()}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index?ncSortBy=year&ncSortDir=asc", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.IndexOf("Earlier Year Scheme", StringComparison.Ordinal) < body.IndexOf("Later Year Scheme", StringComparison.Ordinal));
    }
}
