using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders the Error/NotPermitted/SignedOut views through the full MVC pipeline (rather than only
// via the unit tests in AccountControllerTests). The full Entra ID redirect/callback handshake
// can't be simulated without a live tenant (that's covered at the unit level via
// EntraOpenIdConnectEventsTests instead) - but the authenticated-session view branches and the
// sign-out flow are exercised here via the test factory's sign-in shortcut.
public partial class AccountRouteSmokeTests : IClassFixture<PtlInternalWebTestFactory>
{
    private readonly PtlInternalWebTestFactory _factory;

    public AccountRouteSmokeTests(PtlInternalWebTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_Get_RedirectsToEntraAuthorizeEndpoint()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Account/Login?returnUrl=/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith(PtlInternalWebTestFactory.FakeAuthorizationEndpoint, location);
        Assert.Contains("response_type=code", location);
    }

    [Fact]
    public async Task Login_Post_IsDisabled()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignedOut_RendersConfirmationPageWithSignInLink()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/SignedOut");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You have signed out", body);
        Assert.Contains("govuk-button", body);
        Assert.Contains("Sign in again", body);
        Assert.Contains("href=\"/\"", body);
    }

    [Fact]
    public async Task NotPermitted_RendersFullLayoutWithoutSignOut()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/NotPermitted");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You cannot sign in with this account", body);
        Assert.Contains("<!DOCTYPE html>", body);
        Assert.Contains("govuk-frontend", body);
        // Header/footer/phase-banner render normally - only the "Signed in as/Sign out" panel is
        // absent, which _Header.cshtml already handles on its own (it's conditional on
        // User.Identity.IsAuthenticated, which is false here since no session exists yet).
        Assert.Contains("govuk-phase-banner", body);
        Assert.Contains("govuk-service-navigation", body);
        Assert.DoesNotContain("Sign out", body);
        Assert.DoesNotContain("Signed in as", body);
    }

    [Fact]
    public async Task HomeError_RendersErrorView()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Home/Error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("An error occurred", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticatedSession_ShowsSignedInAsAndSignOutRedirectsToEntraEndSession()
    {
        // WebApplicationFactory's CookieContainer-backed client isn't reliably forwarding cookies
        // across requests in this test host - cookies are collected and forwarded manually instead.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var cookies = new Dictionary<string, string>();

        var signInResponse = await client.GetAsync(PtlInternalWebTestFactory.TestSignInPath);
        CaptureCookies(signInResponse, cookies);

        var homeResponse = await SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        CaptureCookies(homeResponse, cookies);
        var homeBody = await homeResponse.Content.ReadAsStringAsync();
        Assert.Contains("Signed in as", homeBody);
        var token = AntiforgeryTokenRegex().Match(homeBody).Groups[1].Value;

        // Entra ID (unlike CIDM) uses the OIDC handler's default redirect-based sign-out, not a
        // POST-form flow - so the response is a 302 to the end_session_endpoint with id_token_hint
        // carried as a query parameter, not an HTML form body.
        var logoutResponse = await SendWithCookiesAsync(client, HttpMethod.Post, "/Account/Logout", cookies,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        var location = logoutResponse.Headers.Location!.ToString();
        Assert.StartsWith(PtlInternalWebTestFactory.FakeEndSessionEndpoint, location);
        Assert.Contains($"id_token_hint={PtlInternalWebTestFactory.TestIdToken}", location);
    }

    private static void CaptureCookies(HttpResponseMessage response, Dictionary<string, string> cookies)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        foreach (var value in values)
        {
            var nameValue = value.Split(';')[0];
            var separatorIndex = nameValue.IndexOf('=');
            if (separatorIndex > 0)
            {
                cookies[nameValue[..separatorIndex]] = nameValue;
            }
        }
    }

    private static Task<HttpResponseMessage> SendWithCookiesAsync(
        HttpClient client, HttpMethod method, string url, Dictionary<string, string> cookies, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("Cookie", string.Join("; ", cookies.Values));
        return client.SendAsync(request);
    }

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
