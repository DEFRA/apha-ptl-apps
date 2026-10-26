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
}
