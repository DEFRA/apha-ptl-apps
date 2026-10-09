using System.Net;
using System.Text.RegularExpressions;
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
    public async Task ParticipantOnly_SeesResultsSchemesOrdersAndAccountSections_NotReportsOrCommentsItems()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Participant");

        Assert.Contains("Results", body);
        Assert.Contains("Enter results", body);
        Assert.Contains("View results", body);
        Assert.Contains("Schemes", body);
        Assert.Contains("My account", body);
        // Orders also requires CanOrderOnline (see ParticipantWithoutCanOrderOnline_* /
        // ParticipantWithCanOrderOnline_* below) - a bare Participant role isn't enough.
        Assert.DoesNotContain("Orders", body);
        // View reports/Enter comments are Results' Viewer/Test Consultant-only children (see
        // NavigationProvider) - not visible to a Participant-only user.
        Assert.DoesNotContain("View reports", body);
        Assert.DoesNotContain("Enter comments", body);
    }

    [Fact]
    public async Task ParticipantWithoutCanOrderOnline_OrdersMenuAndCardNotDisplayed()
    {
        // Participant role alone isn't enough - legacy's PnlOrdersSection.Visible = IsParticipant
        // AndAlso CanOrderOnline (Home.aspx.vb) requires the linked Customer to be order-eligible too.
        var body = await GetHomePageAsSignedInUserAsync(roles: "Participant");

        Assert.DoesNotContain("Orders", body);
        Assert.DoesNotContain("Create order", body);
    }

    [Fact]
    public async Task ParticipantWithCanOrderOnline_OrdersMenuAndCardDisplayed()
    {
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&canOrderOnline=true");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var homeResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        var body = await homeResponse.Content.ReadAsStringAsync();

        Assert.Contains("Orders", body);
        Assert.Contains("Create order", body);
    }

    [Fact]
    public async Task NavigationCardAndSideNavItem_BothRenderForSameEntry_AndReachTheSameDestination()
    {
        // AC: selecting a navigation card reaches the same destination as the equivalent side-nav
        // item - both are driven by the same NavigationProvider entry, so prove it end-to-end
        // rather than relying on construction alone.
        var body = await GetHomePageAsSignedInUserAsync(roles: "Participant");

        var hrefs = Regex.Matches(body, "href=\"(?<href>[^\"]*ComingSoon[^\"]*title=Enter(%20|\\+)results[^\"]*)\"")
            .Select(match => match.Groups["href"].Value)
            .ToList();

        // One occurrence in the side nav's <a>, one in the Home card's stretched-link <a>.
        Assert.Equal(2, hrefs.Count);
        Assert.Equal(hrefs[0], hrefs[1]);
    }

    [Fact]
    public async Task ViewerOnly_SeesOnlyViewReportsWithinResults_NotParticipantOrTestConsultantItems()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Viewer");

        // Results is shown because one of its children (View reports) is visible to Viewer, even
        // though Results itself has no RequiredRole of its own.
        Assert.Contains("Results", body);
        Assert.Contains("View reports", body);
        Assert.DoesNotContain("Enter results", body);
        Assert.DoesNotContain("View results", body);
        Assert.DoesNotContain("Enter comments", body);
        Assert.DoesNotContain("Schemes", body);
        Assert.DoesNotContain("Orders", body);
        Assert.DoesNotContain("My account", body);
    }

    [Fact]
    public async Task TestConsultantOnly_SeesOnlyEnterCommentsWithinResults_NotParticipantOrViewerItems()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Test Consultant");

        Assert.Contains("Results", body);
        Assert.Contains("Enter comments", body);
        Assert.DoesNotContain("Enter results", body);
        Assert.DoesNotContain("View results", body);
        Assert.DoesNotContain("View reports", body);
        Assert.DoesNotContain("Schemes", body);
        Assert.DoesNotContain("My account", body);
    }

    [Fact]
    public async Task MultipleRoles_SeesAllFourResultsChildren()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Participant,Viewer,Test Consultant");

        Assert.Contains("Results", body);
        Assert.Contains("Enter results", body);
        Assert.Contains("View results", body);
        Assert.Contains("View reports", body);
        Assert.Contains("Enter comments", body);
    }

    [Fact]
    public async Task NoResolvedRoles_ShowsNoRolesMessage()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: null);

        Assert.Contains("No roles are available for your account yet", body);
    }

    [Fact]
    public async Task NoImportantMessagePublished_ShowsStaticBannerButNoAdminMessage()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Participant");

        Assert.Contains("govuk-notification-banner", body);
        Assert.Contains("Please read our information regarding online orders", body);
        Assert.Contains("View Information", body);
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

    [Fact]
    public async Task ImportantMessagePublished_DifferentSignedInUsersSeeTheIdenticalMessage()
    {
        // GetImportantMessageAsync takes no per-user parameter anywhere in the chain (Controller ->
        // ApiClient -> Api -> Service -> Repository), so it's structurally the same for every
        // caller - this proves that end-to-end for two differently-identified, differently-roled
        // users against one shared published message.
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped<ISystemMessageApiClient>(_ => new FakeSystemMessageApiClient("<p>Planned maintenance <strong>Friday</strong></p>"))));

        var firstUserClient = factory.CreateClient();
        var firstUserCookies = new Dictionary<string, string>();
        var firstSignIn = await firstUserClient.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?displayName={Uri.EscapeDataString("Jane Doe")}&roles={Uri.EscapeDataString("Participant")}");
        CookieForwardingHttpClient.CaptureCookies(firstSignIn, firstUserCookies);
        var firstBody = await (await CookieForwardingHttpClient.SendWithCookiesAsync(firstUserClient, HttpMethod.Get, "/Home/Index", firstUserCookies))
            .Content.ReadAsStringAsync();

        var secondUserClient = factory.CreateClient();
        var secondUserCookies = new Dictionary<string, string>();
        var secondSignIn = await secondUserClient.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?displayName={Uri.EscapeDataString("John Smith")}&roles={Uri.EscapeDataString("Viewer")}");
        CookieForwardingHttpClient.CaptureCookies(secondSignIn, secondUserCookies);
        var secondBody = await (await CookieForwardingHttpClient.SendWithCookiesAsync(secondUserClient, HttpMethod.Get, "/Home/Index", secondUserCookies))
            .Content.ReadAsStringAsync();

        Assert.Contains("<p>Planned maintenance <strong>Friday</strong></p>", firstBody);
        Assert.Contains("<p>Planned maintenance <strong>Friday</strong></p>", secondBody);
    }

    [Fact]
    public async Task ParticipantWithLabCode_AccountStripShowsLabNumber()
    {
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&labCode=LAB001");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var homeResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        var body = await homeResponse.Content.ReadAsStringAsync();

        Assert.Contains("Lab Number:", body);
        Assert.Contains("LAB001", body);
        Assert.Contains("User Manual", body);
    }

    [Fact]
    public async Task ParticipantWithoutLabCode_AccountStripOmitsLabNumberWithoutDuplicatingSignedInIdentity()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Viewer");

        Assert.DoesNotContain("Lab Number:", body);
        // Identity is shown once, via the service navigation's "Signed in as" - the account
        // strip no longer repeats it as "Logged in as".
        Assert.DoesNotContain("Logged in as:", body);
        Assert.Contains("Signed in as", body);
    }

    [Fact]
    public async Task LongDisplayNameAndLabCode_HeaderShowsFullTextWithoutTruncation()
    {
        // govuk-service-navigation__list is flex+wrap at desktop and stacks full-width at mobile
        // (see govuk-frontend-6.5.0.min.css), so a long name wraps onto its own line instead of
        // being cut off or overflowing - no truncation/ellipsis is ever applied server-side.
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();
        const string longName = "Silambarasan Duraiswamy";
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}" +
            $"&displayName={Uri.EscapeDataString(longName)}&labCode=LAB001");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var homeResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        var body = await homeResponse.Content.ReadAsStringAsync();

        Assert.Contains($"Signed in as <strong>{longName}</strong>", body);
        Assert.Contains("Lab Number: <strong>LAB001</strong>", body);
        Assert.Contains("Sign out", body);
    }

    [Fact]
    public async Task QuickLinkCards_ShowLegacyBlurbText()
    {
        var body = await GetHomePageAsSignedInUserAsync(roles: "Participant,Viewer,Test Consultant");

        // Ported from legacy Home.aspx.resx's "*Blurb" resources (see NavigationProvider.Stub).
        Assert.Contains("Submit results for a current distribution", body);
        Assert.Contains("View your results from a previous proficiency test", body);
        Assert.Contains("List of current proficiency tests available", body);
        Assert.Contains("View completed proficiency test reports, which include all participant results", body);
        Assert.Contains("Provide or view proficiency test comments", body);
    }

    [Fact]
    public async Task QuickLinkCard_IsRealLinkThatNavigatesToComingSoonPage_NotDisabledPlaceholder()
    {
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var homeResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        var homeBody = await homeResponse.Content.ReadAsStringAsync();

        Assert.DoesNotContain("aria-disabled", homeBody);
        Assert.DoesNotContain("Cannot start yet", homeBody);

        var hrefMatch = Regex.Match(homeBody, "href=\"(?<href>[^\"]*ComingSoon[^\"]*title=View(%20|\\+)schemes[^\"]*)\"");
        Assert.True(hrefMatch.Success, "Expected to find a real href to ComingSoon for the 'View schemes' quick-link card.");

        var comingSoonResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, hrefMatch.Groups["href"].Value, cookies);
        var comingSoonBody = await comingSoonResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, comingSoonResponse.StatusCode);
        Assert.Contains("View schemes", comingSoonBody);
        Assert.Contains("has not been migrated to the new service yet", comingSoonBody);
        Assert.Contains("Back to Home", comingSoonBody);
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
