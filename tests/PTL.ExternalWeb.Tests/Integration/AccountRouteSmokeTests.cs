using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PTL.ExternalWeb.Tests.Integration;

// Renders the Login and Error views through the full MVC pipeline (rather than only via
// controller unit tests) so their GOV.UK error-state branches and the shared _Layout's
// "Signed in as" nav slot are actually exercised. Cookies are handled automatically by
// HttpClient across requests made with the same client instance.
public partial class AccountRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AccountRouteSmokeTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Login_Post_MissingCredentials_RendersErrorSummary()
    {
        var client = _factory.CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Username"] = string.Empty,
            ["Password"] = string.Empty
        }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", body);
    }

    [Fact]
    public async Task Login_Post_Valid_SignsInAndLayoutShowsSignedInAs()
    {
        var client = _factory.CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Username"] = "alice",
            ["Password"] = "secret"
        }));

        var homeResponse = await client.GetAsync("/Home/Index");
        var body = await homeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        Assert.Contains("Signed in as", body);
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

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        return AntiforgeryTokenRegex().Match(body).Groups[1].Value;
    }

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
