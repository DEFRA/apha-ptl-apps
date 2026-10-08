using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

public class HomeRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<PtlInternalWebTestFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PtlInternalWebTestFactory _entraFactory;

    public HomeRouteSmokeTests(WebApplicationFactory<Program> factory, PtlInternalWebTestFactory entraFactory)
    {
        _factory = factory;
        _entraFactory = entraFactory;
    }

    // Default route pattern is {controller=Account}/{action=Login}/{id?}, so "/" itself resolves
    // to Account.Login, which now always challenges Entra ID - redirecting rather than rendering OK.
    [Fact]
    public async Task Root_RedirectsToEntraAuthorizeEndpoint()
    {
        var client = _entraFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(PtlInternalWebTestFactory.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Home_Index_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Home_Error_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Home/Error");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Home_Error_RendersErrorMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Home/Error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("An error occurred", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Account_Login_RedirectsToEntraAuthorizeEndpoint()
    {
        var client = _entraFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(PtlInternalWebTestFactory.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
    }
}
