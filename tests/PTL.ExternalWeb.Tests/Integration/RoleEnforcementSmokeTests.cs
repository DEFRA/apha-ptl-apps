using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Scheme;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Full-pipeline proof that role checks are enforced at the controller boundary (not just hidden
// from nav) - Story 744510. Exercises [Authorize(Policy=...)]-gated controllers via the test
// sign-in mechanism.
public class RoleEnforcementSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public RoleEnforcementSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    [Fact]
    public async Task SchemeIndex_SignedInAsViewerOnly_IsForbidden()
    {
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(new FakeSchemeApiClient());
            })).CreateClient();

        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Viewer")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Scheme/Index", cookies);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SchemeIndex_SignedInAsParticipant_Succeeds()
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TabulationIndex_ParticipantSignedIn_IsForbidden()
    {
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        // "View reports" (TabulationController) is Viewer-only (see NavigationProvider) - a
        // Participant guessing this URL directly, bypassing nav-hiding, must still be refused.
        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, "/Tabulation/Index", cookies);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TabulationIndex_ViewerSignedIn_Succeeds()
    {
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Viewer")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, "/Tabulation/Index", cookies);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
