using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Scheme;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Breadcrumbs should end at the nav's parent section (GOV.UK guidance - the current page itself is
// not included), not a raw controller/action class name - regression coverage for the "Distribution
// > Current"/"Distribution > Previous" bug reported against DistributionController.
public class BreadcrumbTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public BreadcrumbTests(PtlExternalWebTestFactory factory) => _factory = factory;

    [Fact]
    public async Task EnterResults_ShowsResultsAsParentSection_NotControllerActionNames()
    {
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Current", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-breadcrumbs__list-item\">Results</li>", body);
        Assert.DoesNotContain("Distribution</a>", body);
        Assert.DoesNotContain("Current</a>", body);
    }

    [Fact]
    public async Task ViewResults_ShowsResultsAsParentSection_NotControllerActionNames()
    {
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Distribution/Previous", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-breadcrumbs__list-item\">Results</li>", body);
        Assert.DoesNotContain("Distribution</a>", body);
        Assert.DoesNotContain("Previous</a>", body);
    }

    [Fact]
    public async Task ViewSchemes_ShowsSchemesAsParentSection_NotRawControllerName()
    {
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(new FakeSchemeApiClient { ParticipantSchemes = new ParticipantSchemeListResponse([], []) });
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-breadcrumbs__list-item\">Schemes</li>", body);
    }

    [Fact]
    public async Task NotPermitted_RendersNoBreadcrumbAndAPrimarySignOutButton()
    {
        // No sign-in - this page is reached with no local cookie (see AccountController.NotPermitted).
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/NotPermitted?reason=inactive");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("govuk-breadcrumbs", body);
        Assert.Contains("Sign out", body);
        Assert.DoesNotContain("govuk-button--secondary", body);
    }

    [Fact]
    public async Task AccessDenied_RendersNoBreadcrumb()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/AccessDenied");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("govuk-breadcrumbs", body);
    }
}
