using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PTL.ApiClient;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Exercises role-gated Home card and side-navigation visibility through the full MVC pipeline,
// using the test factory's sign-in shortcut (see PtlExternalWebTestFactory) extended with
// displayName/roles query parameters so these tests don't need a real CIDM tenant.
public class HomeNavigationSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public HomeNavigationSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    [Fact]
    public async Task ParticipantOnly_SeesResultsSchemesOrdersAndAccountSections_NotCommentsOrReports()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Participant");

        Assert.Contains("Results", body);
        Assert.Contains("Schemes", body);
        Assert.Contains("Orders", body);
        Assert.Contains("My account", body);
        Assert.DoesNotContain("Comments", body);
        Assert.DoesNotContain("Reports", body);
    }

    [Fact]
    public async Task ViewerOnly_SeesReportsSection_NotParticipantOrTestConsultantSections()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Viewer");

        Assert.Contains("Reports", body);
        Assert.DoesNotContain("Schemes", body);
        Assert.DoesNotContain("Orders", body);
        Assert.DoesNotContain("Comments", body);
    }

    [Fact]
    public async Task TestConsultantOnly_SeesCommentsSection_NotParticipantOrViewerSections()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Test Consultant");

        Assert.Contains("Comments", body);
        Assert.DoesNotContain("Schemes", body);
        Assert.DoesNotContain("Reports", body);
    }

    [Fact]
    public async Task MultipleRoles_SeesEachRolesOwnSections()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Participant,Viewer,Test Consultant");

        Assert.Contains("Results", body);
        Assert.Contains("Reports", body);
        Assert.Contains("Comments", body);
    }

    [Fact]
    public async Task NoResolvedRoles_ShowsNoRolesMessage()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: null);

        Assert.Contains("No roles are available for your account yet", body);
    }

    [Fact]
    public async Task NoImportantMessagePublished_DoesNotShowNotificationBanner()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Participant");

        Assert.DoesNotContain("govuk-notification-banner", body);
    }

    [Fact]
    public async Task ImportantMessagePublished_ShowsNotificationBannerWithPreservedFormatting()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped<ISystemMessageApiClient>(_ => new FakeSystemMessageApiClient("<p>Planned maintenance <strong>Friday</strong></p>"))));
        var client = factory.CreateClient();
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync($"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var homeResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        var body = await homeResponse.Content.ReadAsStringAsync();

        Assert.Contains("govuk-notification-banner", body);
        Assert.Contains("<p>Planned maintenance <strong>Friday</strong></p>", body);
    }

    private async Task<string> GetHomePageAsSignedInUserAsync(string? roles)
    {
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();

        var signInUrl = roles is null
            ? PtlExternalWebTestFactory.TestSignInPath
            : $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString(roles)}";
        var signInResponse = await client.GetAsync(signInUrl);
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var homeResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        return await homeResponse.Content.ReadAsStringAsync();
    }
}
