using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PTL.InternalWeb.Tests.Integration;

// ITHC-005: the shared security headers reach real InternalWeb pages, static files and error pages.
public partial class SecurityHeadersTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("script-src 'self' 'nonce-([^']+)';")]
    private static partial Regex ScriptNonce();

    [Theory]
    [InlineData("/Home/Accessibility")]
    [InlineData("/lib/tinymce/tinymce.min.js")]
    [InlineData("/css/site.css")]
    [InlineData("/no-such-page")]
    public async Task Response_CarriesTheBrowserSecurityHeaders(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Equal(PTL.ApiClient.PtlWebApplicationExtensions.ContentSecurityPolicy(ScriptNonce().Match(policy).Groups[1].Value), policy);
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

    // Every inline script the layout renders carries the nonce the policy allows - so none is refused.
    [Fact]
    public async Task EveryInlineScriptOnThePage_CarriesTheHeadersNonce()
    {
        var response = await factory.CreateClient().GetAsync("/Home/Accessibility");
        var nonce = ScriptNonce().Match(Assert.Single(response.Headers.GetValues("Content-Security-Policy"))).Groups[1].Value;
        var html = await response.Content.ReadAsStringAsync();

        var inline = Regex.Matches(html, "<script\\b(?![^>]*\\bsrc=)[^>]*>", RegexOptions.None, TimeSpan.FromSeconds(1));
        Assert.NotEmpty(inline);
        Assert.All(inline, script => Assert.Contains($"nonce=\"{nonce}\"", script.Value, StringComparison.Ordinal));
        Assert.Contains("/_content/PTL.SharedUI/js/ptl-unobtrusive.js", html, StringComparison.Ordinal);
    }
}
