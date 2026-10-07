using System.Net;
using Microsoft.AspNetCore.Hosting;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

public class SmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public SmokeTests(PtlExternalWebTestFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/Home/Privacy")]
    public async Task Routes_ReturnSuccess(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HomeIndex_Anonymous_RedirectsToLoginPreservingReturnUrl()
    {
        // Unlike Account/Login (which challenges CIDM directly), the app's Cookie scheme uses
        // LoginPath rather than ForwardChallenge, so the FallbackPolicy's automatic challenge for
        // any other protected page redirects to the local Account/Login first (preserving
        // ReturnUrl), which itself then challenges CIDM.
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("/Account/Login", location);
        Assert.Contains("ReturnUrl", location);
    }

    [Fact]
    public async Task HomeIndex_AuthenticatedSession_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var signIn = await client.GetAsync(PtlExternalWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        var response = await client.GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DefaultRoute_ChallengesCidmRatherThanRenderingAForm()
    {
        // The default route is {controller=Account}/{action=Login}/{id?} - an unauthenticated
        // visitor hitting "/" should be sent straight to CIDM, not shown a local form.
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(PtlExternalWebTestFactory.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AccountLogin_Development_CorrelationAndNonceCookiesAreSameSiteLax()
    {
        // Over plain HTTP (local dev) browsers drop SameSite=None cookies that aren't Secure, which
        // makes the CIDM callback fail with "Correlation failed" and land the user on /Home/Error.
        using var devFactory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var client = devFactory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Account/Login?returnUrl=%2FHome%2FPrivacy");

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        var correlation = Assert.Single(setCookies, c => c.StartsWith(".AspNetCore.Correlation.", StringComparison.Ordinal));
        var nonce = Assert.Single(setCookies, c => c.StartsWith(".AspNetCore.OpenIdConnect.Nonce.", StringComparison.Ordinal));
        Assert.Contains("samesite=lax", correlation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", nonce, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccountLogin_ChallengesCidmRatherThanRenderingAForm()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(PtlExternalWebTestFactory.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
    }
}
