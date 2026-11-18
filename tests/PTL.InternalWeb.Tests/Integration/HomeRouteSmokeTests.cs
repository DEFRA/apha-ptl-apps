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
        Assert.Contains("Sorry, there is a problem with the service", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Development", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/No/Such/Page")]
    [InlineData("/Home/NoSuchAction")]
    public async Task UnknownUrl_ShowsPageNotFound_WithA404(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Page not found</h1>", body, StringComparison.Ordinal);
        Assert.Contains("<title>Page not found - Proficiency Testing</title>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-breadcrumbs", body, StringComparison.Ordinal);
        Assert.DoesNotContain("app-side-navigation", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task OtherErrorStatus_ShowsTheProblemWithTheServicePage_KeepingTheStatusCode(int statusCode)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Error/{statusCode}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(statusCode, (int)response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Sorry, there is a problem with the service</h1>", body, StringComparison.Ordinal);
        Assert.Contains("If you contact us, quote this reference:", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownUrl_WhenNotSignedIn_IsSentToSignIn()
    {
        var client = _entraFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/No/Such/Page");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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
