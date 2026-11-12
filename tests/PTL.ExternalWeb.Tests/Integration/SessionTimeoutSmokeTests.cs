using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PTL.Common.SessionTimeout;
using PTL.ExternalWeb.Infrastructure;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Story 746001 - Re-Authenticate After Session Timeout. The countdown itself is client-side
// (session-timeout.js), so these prove the server half: the warning markup, the extend endpoint and the
// Session expired page that sends the user back to CIDM.
public class SessionTimeoutSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public SessionTimeoutSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    private static async Task<(HttpClient Client, Dictionary<string, string> Cookies)> SignedInAsync(HttpClient client)
    {
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync($"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);
        return (client, cookies);
    }

    private async Task<string> GetHomeAsync()
    {
        var (client, cookies) = await SignedInAsync(_factory.CreateClient());
        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task SignedInPage_RendersTheClosedExtendSessionDialogWithAYesButton()
    {
        var body = await GetHomeAsync();

        // A native modal dialog that stays closed (no "open" attribute) until session-timeout.js opens it.
        Assert.Matches("<dialog id=\"session-timeout-warning\"(?![^>]*\\sopen[\\s>=])", body);
        // Dressed as a GOV.UK notification banner: blue header band holding the title, white content below.
        Assert.Contains("govuk-notification-banner app-session-timeout-dialog", body);
        Assert.Matches("<h2 class=\"govuk-notification-banner__title[^\"]*\" id=\"session-timeout-warning-title\"[^>]*>Extend session\\?</h2>", body);
        Assert.Matches("id=\"session-timeout-extend\"[^>]*>\\s*Yes, extend my session\\s*</button>", body);
        Assert.Matches("id=\"session-timeout-signout\" class=\"govuk-button govuk-button--secondary\"[^>]*>\\s*No, sign out\\s*</button>", body);
        Assert.Contains("src=\"/js/session-timeout.js", body);
    }

    [Fact]
    public async Task SignedInPage_DialogIsNamedAndDescribedForScreenReaders()
    {
        var body = await GetHomeAsync();

        Assert.Contains("role=\"alertdialog\"", body);
        Assert.Contains("aria-labelledby=\"session-timeout-warning-title\"", body);
        Assert.Contains("aria-describedby=\"session-timeout-warning-description\"", body);
        Assert.Matches("id=\"session-timeout-warning-description\"", body);
        // The title takes focus when the modal opens (so no button shows its yellow focus state), and the milestone
        // announcements use a polite live region.
        Assert.Matches("id=\"session-timeout-warning-title\" tabindex=\"-1\" autofocus", body);
        Assert.DoesNotMatch("id=\"session-timeout-extend\"[^>]*\\sautofocus", body);
        Assert.Matches("id=\"session-timeout-countdown-announcement\"[^>]*aria-live=\"polite\"", body);
    }

    [Fact]
    public async Task SignedInPage_WarningCarriesTheConfiguredThresholdsAndEndpoints()
    {
        var body = await GetHomeAsync();

        // Whatever the appsettings thresholds are, the dialog carries them through to the script.
        var configured = _factory.Services.GetRequiredService<IOptions<SessionTimeoutOptions>>().Value;
        Assert.Contains($"data-warning-after-ms=\"{(long)configured.InactivityWarningAfter.TotalMilliseconds}\"", body);
        Assert.Contains($"data-expire-after-ms=\"{(long)configured.ExpireAfterWarning.TotalMilliseconds}\"", body);
        Assert.Contains("data-extend-url=\"/Account/ExtendSession\"", body);
        Assert.Contains("data-expired-url=\"/Account/SessionExpired\"", body);
        Assert.Contains("data-signout-url=\"/Account/Logout\"", body);
        Assert.Contains($"<strong id=\"session-timeout-countdown\">{SessionTimeoutText.Duration((int)configured.ExpireAfterWarning.TotalSeconds)}</strong>", body);
        Assert.Matches("data-antiforgery-token=\"[^\"]+\"", body);
    }

    [Fact]
    public async Task AnonymousPage_DoesNotRenderTheWarning()
    {
        var body = await _factory.CreateClient().GetStringAsync("/Home/Privacy");

        Assert.DoesNotContain("session-timeout-warning", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtendSession_SignedInWithAntiforgeryToken_ReturnsNoContent()
    {
        var (client, cookies) = await SignedInAsync(_factory.CreateClient());
        var homeResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        // The page also sets the antiforgery cookie the token is validated against.
        CookieForwardingHttpClient.CaptureCookies(homeResponse, cookies);
        var body = await homeResponse.Content.ReadAsStringAsync();
        var token = Regex.Match(body, "data-antiforgery-token=\"(?<token>[^\"]+)\"").Groups["token"].Value;

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, "/Account/ExtendSession", cookies,
            new FormUrlEncodedContent([new("__RequestVerificationToken", WebUtility.HtmlDecode(token))]));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ExtendSession_WithoutAntiforgeryToken_IsRejected()
    {
        var (client, cookies) = await SignedInAsync(_factory.CreateClient());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, "/Account/ExtendSession", cookies, new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExtendSession_WhenSignedOut_RedirectsToSignIn()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/Account/ExtendSession", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionExpired_WhenAnonymous_ShowsTheMessageWithASignInAgainLink()
    {
        var body = await _factory.CreateClient().GetStringAsync("/Account/SessionExpired");

        Assert.Contains("<h1 class=\"govuk-heading-l\">Session expired</h1>", body);
        // Account/Login is this app's default route, so the generated link to it is the root path.
        Assert.Matches("<a class=\"govuk-button\"[^>]*href=\"/\"[^>]*>Sign in again</a>", body);
        Assert.Contains("<title>Session expired", body);
        // The user has just been signed out, so a Home > Account trail would be misleading.
        Assert.DoesNotContain("govuk-breadcrumbs", body, StringComparison.Ordinal);
        Assert.DoesNotContain("session-timeout-warning", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionExpired_WhenSignedIn_SignsOutLocallyAndShowsTheMessage()
    {
        var (client, cookies) = await SignedInAsync(_factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Account/SessionExpired", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Session expired", body);
        // The header must not still say signed in on the page that reports the session has ended.
        Assert.DoesNotContain("Signed in as", body, StringComparison.Ordinal);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);

        // Local cookie only: the response clears the app cookie but never redirects to CIDM's end-session
        // endpoint, so a live CIDM session can sign the user straight back in when they select OK.
        CookieForwardingHttpClient.CaptureCookies(response, cookies);
        var homeResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        Assert.Equal(HttpStatusCode.Redirect, homeResponse.StatusCode);
        Assert.Contains("/Account/Login", homeResponse.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignInAgainOnSessionExpired_StartsTheCidmSignInChallenge()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(PtlExternalWebTestFactory.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
    }
}
