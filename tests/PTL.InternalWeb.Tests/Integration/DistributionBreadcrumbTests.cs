using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Breadcrumb trail for the Distribution screens (legacy Web.sitemap: Distributions > Sample
// Preparation / Sample Packaging / Results Receipt > Results, Monthly Distributions > ...).
public class DistributionBreadcrumbTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid Scheme = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid Participant = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public DistributionBreadcrumbTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private async Task<List<(string Text, string? Href)>> CrumbsAsync(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        var nav = Regex.Match(html, "<nav class=\"govuk-breadcrumbs\".*?</nav>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(nav);

        return [.. Regex.Matches(nav, "<li class=\"govuk-breadcrumbs__list-item\">(.*?)</li>", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value)
            .Select(inner => (
                Text: WebUtility.HtmlDecode(Regex.Replace(inner, "<[^>]+>", string.Empty)).Trim(),
                Href: Regex.Match(inner, "href=\"([^\"]+)\"") is { Success: true } href ? WebUtility.HtmlDecode(href.Groups[1].Value) : null))];
    }

    [Fact]
    public async Task ResultsReceipt_ShowsHomeDistributionsResultsReceipt_WithTheYearKeptOnTheDistributionsLink()
    {
        _apiClient.ResultsReceipt = new ResultsReceiptResponse(Guid.NewGuid(), 2026, 5, []);

        var crumbs = await CrumbsAsync("/Distribution/ResultsReceipt?year=2026&monthId=5");

        Assert.Equal(["Home", "Distributions", "Results Receipt"], crumbs.Select(c => c.Text));
        Assert.Contains("/Distribution", crumbs[1].Href, StringComparison.Ordinal);
        Assert.Contains("year=2026", crumbs[1].Href, StringComparison.Ordinal);
        Assert.Null(crumbs[2].Href);
    }

    [Fact]
    public async Task ActualResults_LinksBackToTheResultsReceiptForTheSameMonth()
    {
        _apiClient.ActualResults = new ActualResultsResponse(Participant, Scheme, "D1 S1: Brucella", "Audit", new DateTime(2030, 1, 1), false, []);

        var crumbs = await CrumbsAsync($"/Distribution/ActualResults?year=2026&monthId=5&schemeId={Scheme}&participantId={Participant}");

        Assert.Equal(["Home", "Distributions", "Results Receipt", "Results"], crumbs.Select(c => c.Text));
        Assert.Contains("/Distribution/ResultsReceipt", crumbs[2].Href, StringComparison.Ordinal);
        Assert.Contains("year=2026", crumbs[2].Href, StringComparison.Ordinal);
        Assert.Contains("monthId=5", crumbs[2].Href, StringComparison.Ordinal);
        Assert.Null(crumbs[3].Href);
    }

    [Fact]
    public async Task SampleReceipt_LinksBackToTheResultsReceiptForTheSameMonth()
    {
        _apiClient.SampleReceipt = new SampleReceiptResponse("D1 : BT Brucella", "Labcode L001: Lab One", true, []);

        var crumbs = await CrumbsAsync($"/Distribution/SampleReceipt?year=2026&monthId=5&schemeId={Scheme}&participantId={Participant}");

        Assert.Equal(["Home", "Distributions", "Results Receipt", "Submission Status"], crumbs.Select(c => c.Text));
        Assert.Contains("/Distribution/ResultsReceipt", crumbs[2].Href, StringComparison.Ordinal);
        Assert.Contains("year=2026", crumbs[2].Href, StringComparison.Ordinal);
        Assert.Contains("monthId=5", crumbs[2].Href, StringComparison.Ordinal);
        Assert.Null(crumbs[3].Href);
    }

    [Fact]
    public async Task Packaging_ShowsSamplePackagingInsteadOfAPlainControllerAndActionCrumb()
    {
        var crumbs = await CrumbsAsync("/Distribution/Packaging?year=2026&monthId=5");

        Assert.Equal(["Home", "Distributions", "Sample Packaging"], crumbs.Select(c => c.Text));
        Assert.Contains("year=2026", crumbs[1].Href, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntendedResults_LinksMonthlyDistributionsBackToTheSameMonth()
    {
        _apiClient.IntendedResults = new IntendedResultsResponse(Scheme, "D1a", "S1", "Brucella", RequiresAuditReason: false, []);

        var crumbs = await CrumbsAsync($"/Distribution/IntendedResults?year=2026&monthId=5&schemeId={Scheme}");

        Assert.Equal(["Home", "Distributions", "Monthly Distributions", "Intended Results"], crumbs.Select(c => c.Text));
        Assert.Contains("/Distribution/Schedule", crumbs[2].Href, StringComparison.Ordinal);
        Assert.Contains("year=2026", crumbs[2].Href, StringComparison.Ordinal);
        Assert.Contains("monthId=5", crumbs[2].Href, StringComparison.Ordinal);
    }
}
