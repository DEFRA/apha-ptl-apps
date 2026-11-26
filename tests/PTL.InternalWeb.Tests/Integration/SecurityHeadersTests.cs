using Microsoft.AspNetCore.Mvc.Testing;

namespace PTL.InternalWeb.Tests.Integration;

// ITHC-005: the shared security headers reach real InternalWeb pages, static files and error pages.
public class SecurityHeadersTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/Home/Accessibility")]
    [InlineData("/lib/tinymce/tinymce.min.js")]
    [InlineData("/css/site.css")]
    [InlineData("/no-such-page")]
    public async Task Response_CarriesTheBrowserSecurityHeaders(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(PTL.ApiClient.PtlWebApplicationExtensions.ContentSecurityPolicy, Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task Page_StillRendersUnderTheNewHeaders()
    {
        var html = await factory.CreateClient().GetStringAsync("/Home/Accessibility");

        Assert.Contains("Accessibility statement", html, StringComparison.Ordinal);
    }
}
