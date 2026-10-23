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
        var html = await HtmlAsync(url);

        var nav = Regex.Match(html, "<nav class=\"govuk-breadcrumbs\".*?</nav>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(nav);

        return [.. Regex.Matches(nav, "<li class=\"govuk-breadcrumbs__list-item\">(.*?)</li>", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value)
            .Select(inner => (
                Text: WebUtility.HtmlDecode(Regex.Replace(inner, "<[^>]+>", string.Empty)).Trim(),
                Href: Regex.Match(inner, "href=\"([^\"]+)\"") is { Success: true } href ? WebUtility.HtmlDecode(href.Groups[1].Value) : null))];
    }

    private async Task<string> HtmlAsync(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
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
    public async Task Tabulations_ShowsHomeDistributionsTabulations_WithTheYearKeptOnTheDistributionsLink()
    {
        _apiClient.Tabulations = new TabulationsResponse(2026, 5, []);

        var crumbs = await CrumbsAsync("/Distribution/Tabulations?year=2026&monthId=5");

        Assert.Equal(["Home", "Distributions", "Tabulations"], crumbs.Select(c => c.Text));
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

    // Search has no year/month context at all (legacy Distributions/Search.aspx), so the
    // Distributions ancestor crumb must still be a working link, just without a year carried onto
    // it - the Dashboard defaults its own year when none is supplied.
    [Fact]
    public async Task Search_ShowsHomeDistributionsSearch_WithDistributionsStillLinkedButWithNoYear()
    {
        var crumbs = await CrumbsAsync("/Distribution/Search");

        Assert.Equal(["Home", "Distributions", "Search"], crumbs.Select(c => c.Text));
        Assert.NotNull(crumbs[1].Href);
        Assert.DoesNotContain("year", crumbs[1].Href, StringComparison.Ordinal);
        Assert.Null(crumbs[2].Href);
    }

    // Same sibling pattern as Search - no year/month context, legacy Distributions/ParticipantAddress.aspx.
    [Fact]
    public async Task ParticipantAddressLabels_ShowsHomeDistributionsParticipantAddressLabels_AndRendersTheHeading()
    {
        var html = await HtmlAsync("/Distribution/ParticipantAddressLabels");
        var crumbs = await CrumbsAsync("/Distribution/ParticipantAddressLabels");

        Assert.Contains("<h1 class=\"govuk-heading-l\">Participant Address Labels</h1>", html, StringComparison.Ordinal);
        Assert.Equal(["Home", "Distributions", "Participant Address Labels"], crumbs.Select(c => c.Text));
        Assert.NotNull(crumbs[1].Href);
        Assert.Null(crumbs[2].Href);
    }

    [Fact]
    public async Task Preparation_HasNoBackLinkBecauseTheBreadcrumbsLeadBack()
    {
        _apiClient.Preparation = new PreparationSheetResponse(Guid.NewGuid(), 2026, 5, []);

        var html = await HtmlAsync("/Distribution/Preparation?year=2026&monthId=5");

        Assert.DoesNotContain("govuk-back-link", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Packaging_HasNoBackLinkBecauseTheBreadcrumbsLeadBack()
    {
        var html = await HtmlAsync("/Distribution/Packaging?year=2026&monthId=5");

        Assert.DoesNotContain("govuk-back-link", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResultsReceipt_HasNoBackLinkBecauseTheBreadcrumbsLeadBack()
    {
        _apiClient.ResultsReceipt = new ResultsReceiptResponse(Guid.NewGuid(), 2026, 5, []);

        var html = await HtmlAsync("/Distribution/ResultsReceipt?year=2026&monthId=5");

        Assert.DoesNotContain("govuk-back-link", html, StringComparison.Ordinal);
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

    private static readonly Guid Tabulation = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000b1");
    private static readonly Guid PublishedTabulation = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000b2");
    private static readonly Guid Test = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000b3");

    private static TabulationReportResponse TabulationReport() =>
        new(Tabulation, false, Tabulation, "FINAL REPORT", "Summary", "Date Of Distribution: 03/05/2026", "", "", "", "Comments :", false, null,
            [], Guid.Empty, [], Guid.Empty, [], null, null, new TabulationPrintResponse([], [], []));

    private static AssessmentResultsResponse AssessmentResults() => new(false, [], "Assessment Score", "Frequency", []);

    private static void AssertMonthLink(string? href, string path)
    {
        Assert.StartsWith(path + "?", href, StringComparison.Ordinal);
        Assert.Contains("year=2026", href, StringComparison.Ordinal);
        Assert.Contains("monthId=5", href, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_ShowsTabulationUnderTabulationsForTheSameMonth()
    {
        _apiClient.TabulationReport = TabulationReport();

        var crumbs = await CrumbsAsync($"/Distribution/Tabulation?year=2026&monthId=5&tabulationId={Tabulation}");

        Assert.Equal(["Home", "Distributions", "Tabulations", "Tabulation"], crumbs.Select(c => c.Text));
        AssertMonthLink(crumbs[2].Href, "/Distribution/Tabulations");
        Assert.Null(crumbs[3].Href);
    }

    [Fact]
    public async Task AssessmentResults_LinksBackToTheTabulationItCameFrom()
    {
        _apiClient.AssessmentResults = AssessmentResults();

        var crumbs = await CrumbsAsync($"/Distribution/TabulationAssessmentResults?year=2026&monthId=5&tabulationId={Tabulation}&testId={Test}");

        Assert.Equal(["Home", "Distributions", "Tabulations", "Tabulation", "Assessment Results"], crumbs.Select(c => c.Text));
        AssertMonthLink(crumbs[2].Href, "/Distribution/Tabulations");
        AssertMonthLink(crumbs[3].Href, "/Distribution/Tabulation");
        Assert.Contains($"tabulationId={Tabulation}", crumbs[3].Href, StringComparison.Ordinal);
        Assert.DoesNotContain("testId", crumbs[3].Href, StringComparison.Ordinal);
        Assert.Null(crumbs[4].Href);
    }

    [Fact]
    public async Task ViewComments_ShowsViewCommentsUnderTabulationsForTheSameMonth()
    {
        _apiClient.TestConsultantComments = new TestConsultantCommentsResponse(
            Scheme, "D1a/S1 BT: Brucella", "", "", true, [], [".pdf"], "PDF (.pdf)", "10MB");

        var crumbs = await CrumbsAsync($"/Distribution/ViewComments?year=2026&monthId=5&schemeId={Scheme}");

        Assert.Equal(["Home", "Distributions", "Tabulations", "View Comments"], crumbs.Select(c => c.Text));
        AssertMonthLink(crumbs[2].Href, "/Distribution/Tabulations");
        Assert.Null(crumbs[3].Href);
    }

    [Fact]
    public async Task AmendResults_ShowsAmendResultsUnderTabulationsForTheSameMonth()
    {
        _apiClient.AmendResults = new AmendResultsResponse(Scheme, "D1a/S1 BT: Brucella", false, false, [], [], [], [], []);

        var crumbs = await CrumbsAsync($"/Distribution/AmendResults?year=2026&monthId=5&schemeId={Scheme}");

        Assert.Equal(["Home", "Distributions", "Tabulations", "Amend Results"], crumbs.Select(c => c.Text));
        AssertMonthLink(crumbs[2].Href, "/Distribution/Tabulations");
        Assert.Null(crumbs[3].Href);
    }

    [Fact]
    public async Task PublishedTabulations_ShowsIssuedTabulationsUnderTabulations()
    {
        _apiClient.PublishedTabulations = new PublishedTabulationsResponse(Tabulation, "D1 BT: Brucella", "Summary", []);

        var crumbs = await CrumbsAsync($"/Distribution/PublishedTabulations?year=2026&monthId=5&tabulationId={Tabulation}");

        Assert.Equal(["Home", "Distributions", "Tabulations", "Issued Tabulations"], crumbs.Select(c => c.Text));
        AssertMonthLink(crumbs[2].Href, "/Distribution/Tabulations");
        Assert.Null(crumbs[3].Href);
    }

    [Fact]
    public async Task PublishedTabulation_LinksBackToTheIssuedTabulationsOfTheSameTabulation()
    {
        _apiClient.PublishedTabulationReport = TabulationReport();

        var crumbs = await CrumbsAsync($"/Distribution/PublishedTabulation?year=2026&monthId=5&tabulationId={Tabulation}&publishedTabulationId={PublishedTabulation}");

        Assert.Equal(["Home", "Distributions", "Tabulations", "Issued Tabulations", "Issued Tabulation"], crumbs.Select(c => c.Text));
        AssertMonthLink(crumbs[3].Href, "/Distribution/PublishedTabulations");
        Assert.Contains($"tabulationId={Tabulation}", crumbs[3].Href, StringComparison.Ordinal);
        Assert.DoesNotContain("publishedTabulationId", crumbs[3].Href, StringComparison.Ordinal);
        Assert.Null(crumbs[4].Href);
    }

    [Fact]
    public async Task PublishedAssessmentResults_LinksBackToTheIssuedTabulationItCameFrom()
    {
        _apiClient.AssessmentResults = AssessmentResults();

        var crumbs = await CrumbsAsync(
            $"/Distribution/PublishedAssessmentResults?year=2026&monthId=5&tabulationId={Tabulation}&publishedTabulationId={PublishedTabulation}&testId={Test}");

        Assert.Equal(
            ["Home", "Distributions", "Tabulations", "Issued Tabulations", "Issued Tabulation", "Issued Assessment Results"],
            crumbs.Select(c => c.Text));
        AssertMonthLink(crumbs[4].Href, "/Distribution/PublishedTabulation");
        Assert.Contains($"tabulationId={Tabulation}", crumbs[4].Href, StringComparison.Ordinal);
        Assert.Contains($"publishedTabulationId={PublishedTabulation}", crumbs[4].Href, StringComparison.Ordinal);
        Assert.DoesNotContain("testId", crumbs[4].Href, StringComparison.Ordinal);
        Assert.Null(crumbs[5].Href);
    }
}
