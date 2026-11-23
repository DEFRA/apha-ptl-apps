using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PTL.Api.Tests.TestSupport;
using PTL.Common.ApiAuthentication;

namespace PTL.Api.Tests.Endpoints;

// Drives the real pipeline (authentication + fallback authorization policy) through the trivial "/" endpoint.
public class ApiKeyAuthenticationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<HttpResponseMessage> GetAsync(string path, string? apiKey, string? clientApp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (apiKey is not null)
        {
            request.Headers.Add(ApiAuthenticationOptions.ApiKeyHeaderName, apiKey);
        }

        if (clientApp is not null)
        {
            request.Headers.Add(ApiAuthenticationOptions.ClientAppHeaderName, clientApp);
        }

        return await factory.CreateClient().SendAsync(request);
    }

    [Theory]
    [InlineData(ApiAuthenticationOptions.InternalWeb)]
    [InlineData(ApiAuthenticationOptions.ExternalWeb)]
    public async Task ValidKeyAndKnownClientApp_IsAuthorized(string clientApp)
    {
        var response = await GetAsync("/", TestHostConfiguration.ApiKey, clientApp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello World!", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task MissingApiKey_IsUnauthorized(string? apiKey)
    {
        var response = await GetAsync("/", apiKey, ApiAuthenticationOptions.InternalWeb);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("TEST-API-KEY")]
    public async Task InvalidApiKey_IsUnauthorized(string apiKey)
    {
        var response = await GetAsync("/", apiKey, ApiAuthenticationOptions.InternalWeb);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidApiKey_WithoutClientApp_IsStillUnauthorized()
    {
        var response = await GetAsync("/", "wrong-key", clientApp: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingClientApp_IsForbidden()
    {
        var response = await GetAsync("/", TestHostConfiguration.ApiKey, clientApp: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("internalweb")]
    [InlineData("InternalWeb,ExternalWeb")]
    public async Task InvalidClientApp_IsForbidden(string clientApp)
    {
        var response = await GetAsync("/", TestHostConfiguration.ApiKey, clientApp);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ControllerEndpoint_WithoutHeaders_IsUnauthorized()
    {
        var response = await GetAsync("/api/search/participants", apiKey: null, clientApp: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LivenessEndpoint_StaysAnonymous()
    {
        var response = await GetAsync("/health", apiKey: null, clientApp: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
