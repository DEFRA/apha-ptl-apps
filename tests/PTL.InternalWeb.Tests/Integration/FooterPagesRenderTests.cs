using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Cookies, Accessibility statement and Privacy are anonymous footer pages: breadcrumbs stay, the left-hand navigation goes.
public class FooterPagesRenderTests(PtlInternalWebTestFactory factory) : IClassFixture<PtlInternalWebTestFactory>
{
    private HttpClient AnonymousClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/Home/Cookies", "Cookies on Proficiency Testing", "Cookies")]
    [InlineData("/Home/Accessibility", "Accessibility statement for the PT", "Accessibility")]
    [InlineData("/Home/Privacy", "VETQAS privacy notice", "Privacy")]
    public async Task FooterPage_IsAnonymous_KeepsBreadcrumbs_AndHidesSideNavigation(string url, string heading, string crumb)
    {
        var response = await AnonymousClient().GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(heading, html, StringComparison.Ordinal);
        Assert.Contains("govuk-breadcrumbs", html, StringComparison.Ordinal);
        Assert.Contains($"govuk-breadcrumbs__list-item\">{crumb}</li>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("app-side-navigation", html, StringComparison.Ordinal);
        Assert.Contains("govuk-grid-column-full", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CookiesPage_ListsEssentialCookiesOnly()
    {
        var html = await AnonymousClient().GetStringAsync("/Home/Cookies");

        Assert.Contains("only uses essential cookies", html, StringComparison.Ordinal);
        Assert.Contains(".AspNetCore.Cookies", html, StringComparison.Ordinal);
        Assert.Contains(".AspNetCore.Antiforgery.*", html, StringComparison.Ordinal);
        Assert.Contains("ptl-contract-search", html, StringComparison.Ordinal);
        Assert.DoesNotContain("analytics", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Footer_LinksStayInsideInternalWeb()
    {
        var html = await AnonymousClient().GetStringAsync("/Home/Cookies");

        Assert.Contains("href=\"/Home/Cookies\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Home/Accessibility\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Home/Privacy\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/docs/TermsAndConditions.pdf\"", html, StringComparison.Ordinal);
    }
}
