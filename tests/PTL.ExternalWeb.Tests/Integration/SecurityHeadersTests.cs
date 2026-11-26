using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// ITHC-005: the shared security headers reach real ExternalWeb pages, static files and error pages.
public class SecurityHeadersTests(PtlExternalWebTestFactory factory) : IClassFixture<PtlExternalWebTestFactory>
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
