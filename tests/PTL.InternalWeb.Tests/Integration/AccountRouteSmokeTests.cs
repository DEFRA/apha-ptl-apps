using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PTL.InternalWeb.Tests.Integration;

// Renders the Login view through the full MVC pipeline (rather than only via the unit tests in
// AccountControllerTests) so its GOV.UK error-state branches and the shared _Layout's
// "Signed in as" nav slot are actually exercised. Cookies (antiforgery + auth) are handled
// automatically by HttpClient across requests made with the same client instance.
public class AccountRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
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

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        return Regex.Match(body, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }
}
