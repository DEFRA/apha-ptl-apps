using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Renders the Error view through the full MVC pipeline (rather than only via controller unit
// tests) so its GOV.UK error-state branches are actually exercised. The full CIDM redirect/callback
// handshake can't be simulated without a live tenant (that's covered at the unit level via
// CidmOpenIdConnectEventsTests instead) - but the authenticated-session view branches and the
// sign-out flow are exercised here via the test factory's sign-in shortcut.
public partial class AccountRouteSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public AccountRouteSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_Get_RedirectsToCidmAuthorizeEndpoint()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Account/Login?returnUrl=/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith(PtlExternalWebTestFactory.FakeAuthorizationEndpoint, location);
        Assert.Contains("serviceId=test-service-id", location);
        Assert.Contains("response_type=code", location);
    }

    [Fact]
    public async Task Login_Post_IsDisabled()
    {
        var client = _factory.CreateClient();

        // The base class's [ValidateAntiForgeryToken] is still picked up by reflection on this
        // override even without redeclaring it, so a token-less POST is rejected before it can
        // reach the NotFound() body - either way, no sign-in ever happens on this path.
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
    }

    [Fact]
    public async Task HomeError_RendersErrorView()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Home/Error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("An error occurred while processing your request.", body);
    }

    [Fact]
    public async Task AuthenticatedSession_ShowsSignedInAsAndSignOutFormRendersCidmPost()
    {
        // WebApplicationFactory's CookieContainer-backed client isn't reliably forwarding cookies
        // across requests in this test host (observed even after aligning the client's scheme with
        // the Secure cookie policy) - cookies are collected and forwarded manually instead, which is
        // simpler and more predictable than chasing that down further.
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();

        var signInResponse = await client.GetAsync(PtlExternalWebTestFactory.TestSignInPath);
        CaptureCookies(signInResponse, cookies);

        var homeResponse = await SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        CaptureCookies(homeResponse, cookies);
        var homeBody = await homeResponse.Content.ReadAsStringAsync();
        Assert.Contains("Signed in as", homeBody);
        var token = AntiforgeryTokenRegex().Match(homeBody).Groups[1].Value;

        var logoutResponse = await SendWithCookiesAsync(client, HttpMethod.Post, "/Account/Logout", cookies,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        var logoutBody = await logoutResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        Assert.Contains($"action=\"{PtlExternalWebTestFactory.FakeEndSessionEndpoint}\"", logoutBody);
        Assert.Contains("id_token_hint", logoutBody);
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

