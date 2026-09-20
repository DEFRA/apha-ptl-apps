using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline tests for the OIDC sign-in/sign-out entry points. A real Entra ID metadata
// document is never fetched - see OidcTestMetadataExtensions for why.
public class AccountRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AccountRouteSmokeTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Login_RedirectsToEntraIdAuthorizationEndpoint()
    {
        var client = _factory.WithoutRealOidcDiscovery()
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(OidcTestMetadataExtensions.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AccessDenied_IsReachable_WithoutAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/AccessDenied");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Access denied", body);
    }

    [Fact]
    public async Task Logout_WhenAuthenticated_SignsOutAndRedirects()
    {
        var client = _factory.WithTestAuthentication().WithoutRealOidcDiscovery()
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var homeResponse = await client.GetAsync("/Home/Index");
        var body = await homeResponse.Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(body, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(OidcTestMetadataExtensions.FakeEndSessionEndpoint, response.Headers.Location!.ToString());
    }
}
