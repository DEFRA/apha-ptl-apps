using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

public class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Home/Index")]
    [InlineData("/Home/Privacy")]
    public async Task ProtectedRoutes_WhenAnonymous_RedirectToSignIn(string url)
    {
        var client = _factory.WithoutRealOidcDiscovery()
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(OidcTestMetadataExtensions.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/Home/Index")]
    [InlineData("/Home/Privacy")]
    public async Task ProtectedRoutes_WhenAuthenticated_ReturnSuccess(string url)
    {
        var client = _factory.WithTestAuthentication().CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_IsReachable_WithoutAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

