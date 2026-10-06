using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PTL.InternalWeb.Tests.Integration;

// Guards the GOV.UK page template structure in _Layout.cshtml - the skip link is only useful if
// <main> starts at the page's own content, and every page needs its own title.
public class LayoutStructureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LayoutStructureTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<string> GetHomePageAsync()
    {
        var response = await _factory.CreateClient().GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task SkipLinkTarget_IsWhereThePageContentStarts()
    {
        var html = await GetHomePageAsync();

        var main = html[html.IndexOf("id=\"main-content\"", StringComparison.Ordinal)..];
        var mainEnd = main.IndexOf("</main>", StringComparison.Ordinal);
        Assert.True(mainEnd > 0, "The layout should render a closing </main>.");

        var mainContent = main[..mainEnd];
        Assert.DoesNotContain("Primary navigation", mainContent, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-breadcrumbs", mainContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NavigationAndBreadcrumbs_AreStillRendered()
    {
        var html = await GetHomePageAsync();

        Assert.Contains("Primary navigation", html, StringComparison.Ordinal);
        Assert.Contains("id=\"main-content\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PageTitle_NamesThePageAndTheService()
    {
        var html = await GetHomePageAsync();

        Assert.Contains("<title>Home - Proficiency Testing - Internal</title>", html, StringComparison.Ordinal);
    }
}
