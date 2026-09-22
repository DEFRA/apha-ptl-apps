using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PTL.InternalWeb.Tests.Integration;

/// <summary>
/// Enhanced Menu and Home route tests to exercise all navigation and error handling paths.
/// Ensures menu and home views have complete code path coverage.
/// </summary>
public class MenuAndHomeRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MenuAndHomeRouteSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Menu_ManageContracts_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Menu/ManageContracts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Menu_ManageSchemes_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Menu/ManageSchemes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Menu_ManageContracts_RendersHeading()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Menu/ManageContracts");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-heading", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Menu_ManageSchemes_RendersHeading()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Menu/ManageSchemes");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-heading", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Home_Index_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Home_Privacy_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Home/Privacy");

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
    public async Task Home_Privacy_RendersContent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Home/Privacy");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.Length > 0, "Privacy page should have content");
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
