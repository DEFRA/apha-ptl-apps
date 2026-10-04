using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PTL.InternalWeb.Tests.Integration;

public class HomeRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HomeRouteSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // Default route pattern is {controller=Account}/{action=Login}/{id?}, so "/" itself resolves
    // to Account.Login, not Home.Index - kept separate from Home_Index_ReturnsSuccess below, which
    // hits Home.Index explicitly.
    [Fact]
    public async Task Root_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
    public async Task Account_Login_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Account_Login_RendersForm()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/Login");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-form-group", body);
    }
}
