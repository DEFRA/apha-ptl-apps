using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PTL.ApiClient.Tests;

// ITHC-005: browser security headers applied by UsePtlWebFrontEnd to every response of both web front-ends.
public class SecurityHeadersTests
{
    private const string ExpectedPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self' data:; connect-src 'self'; object-src 'none'; " +
        "base-uri 'self'; frame-ancestors 'none'";

    [Theory]
    [InlineData("Production", "/probe", HttpStatusCode.OK)]
    [InlineData("Production", "/missing", HttpStatusCode.NotFound)]
    [InlineData("Production", "/boom", HttpStatusCode.InternalServerError)]
    [InlineData("Development", "/probe", HttpStatusCode.OK)]
    [InlineData("Development", "/missing", HttpStatusCode.NotFound)]
    public async Task EveryResponse_CarriesTheBrowserSecurityHeaders(string environment, string path, HttpStatusCode expectedStatus)
    {
        await using var app = await StartAppAsync(environment);

        var response = await app.GetTestClient().GetAsync(path);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(ExpectedPolicy, Single(response, "Content-Security-Policy"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Single(response, "Referrer-Policy"));
    }

    [Fact]
    public async Task ExceptionResponse_IsStillTheFriendlyErrorPage()
    {
        await using var app = await StartAppAsync("Production");

        var response = await app.GetTestClient().GetAsync("/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("error page", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Hsts_IsSentForAnHttpsRequestForwardedByTheAlb()
    {
        await using var app = await StartAppAsync("Production");
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://ptl.example");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        var response = await client.GetAsync("/probe");

        Assert.Equal("max-age=31536000", Single(response, "Strict-Transport-Security"));
    }

    [Fact]
    public async Task Hsts_IsSentForADirectHttpsRequest()
    {
        await using var app = await StartAppAsync("Production");
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://ptl.example");

        var response = await client.GetAsync("/probe");

        Assert.Equal("max-age=31536000", Single(response, "Strict-Transport-Security"));
    }

    [Fact]
    public async Task Hsts_IsNotSentOverPlainHttp()
    {
        await using var app = await StartAppAsync("Production");
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://ptl.example");

        var response = await client.GetAsync("/probe");

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Hsts_IsNotSentInDevelopment()
    {
        await using var app = await StartAppAsync("Development");
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://ptl.example");

        var response = await client.GetAsync("/probe");

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    // Antiforgery adds X-Frame-Options: SAMEORIGIN to pages with forms unless told not to.
    [Fact]
    public async Task FormPage_HasASingleDenyFrameOptionsValue()
    {
        await using var app = await StartAppAsync("Production");

        var response = await app.GetTestClient().GetAsync("/form");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.True(app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.SuppressXFrameOptionsHeader);
    }

    [Fact]
    public async Task EndpointThatSetsItsOwnHeader_KeepsIt()
    {
        await using var app = await StartAppAsync("Production");

        var response = await app.GetTestClient().GetAsync("/own-policy");

        Assert.Equal("default-src 'none'", Single(response, "Content-Security-Policy"));
    }

    private static string Single(HttpResponseMessage response, string name)
    {
        var values = response.Headers.TryGetValues(name, out var h) ? h
            : response.Content.Headers.TryGetValues(name, out var c) ? c
            : [];
        return Assert.Single(values);
    }

    private static async Task<WebApplication> StartAppAsync(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration["Api:BaseUrl"] = "http://localhost:5252";
        builder.Configuration["ApiAuthentication:ApiKey"] = "test-api-key";
        builder.Configuration["ApiAuthentication:ClientApp"] = "InternalWeb";
        builder.AddPtlWebFrontEnd();

        var app = builder.Build();
        app.UsePtlWebFrontEnd();
        app.MapGet("/probe", () => "ok");
        app.MapGet("/boom", string () => throw new InvalidOperationException("boom"));
        app.MapGet("/Home/Error", () => "error page");
        app.MapGet("/form", (HttpContext context, IAntiforgery antiforgery) =>
        {
            antiforgery.GetAndStoreTokens(context);
            return "form";
        });
        app.MapGet("/own-policy", (HttpContext context) =>
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'";
            return "own";
        });

        await app.StartAsync();
        return app;
    }
}
