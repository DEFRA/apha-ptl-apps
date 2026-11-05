using System.Net;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Legacy Help.aspx: role-gated areas of guidance and a context-sensitive footer link.
public class HelpSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public HelpSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    private async Task<(HttpStatusCode Status, string Body)> GetAsync(string path, string roles)
    {
        var client = _factory.CreateClient();
        var cookies = new Dictionary<string, string>();
        var signIn = await client.GetAsync($"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString(roles)}");
        CookieForwardingHttpClient.CaptureCookies(signIn, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, path, cookies);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Participant_SeesGeneralAndParticipantGuidanceIncludingTheResultFormats()
    {
        var (status, body) = await GetAsync("/Help/Index", "Participant,Viewer");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("id=\"contextHelpHome\"", body, StringComparison.Ordinal);
        Assert.Contains("id=\"contextHelpCurrentdistributions\"", body, StringComparison.Ordinal);
        Assert.Contains("id=\"contextHelpResultsentry\"", body, StringComparison.Ordinal);
        Assert.Contains("id=\"contextHelpPreviousdistributions\"", body, StringComparison.Ordinal);
        Assert.Contains("id=\"contextHelpViewresults\"", body, StringComparison.Ordinal);
        Assert.Contains(">dd/mm/yyyy</td>", body, StringComparison.Ordinal);
        Assert.Contains(">Drop down list</td>", body, StringComparison.Ordinal);
        Assert.Contains(">Percentage</td>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("contextHelpEntercomments", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Viewer_SeesOnlyReportViewingGuidance()
    {
        var (_, body) = await GetAsync("/Help/Index", "Viewer");

        Assert.Contains("id=\"contextHelpReports\"", body, StringComparison.Ordinal);
        Assert.Contains("id=\"contextHelpViewreports\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("contextHelpResultsentry", body, StringComparison.Ordinal);
        Assert.DoesNotContain("contextHelpEntercomments", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestConsultant_SeesCommentsGuidance()
    {
        var (_, body) = await GetAsync("/Help/Index", "Test Consultant,Viewer");

        Assert.Contains("id=\"contextHelpDistributions\"", body, StringComparison.Ordinal);
        Assert.Contains("id=\"contextHelpEntercomments\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("contextHelpResultsentry", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FooterHelpLink_OnHome_PointsAtTheHomeSectionOfHelp()
    {
        var (_, body) = await GetAsync("/Home/Index", "Participant");

        Assert.Contains("href=\"/Help/Index#contextHelpHome\" aria-label=\"View our help documentation\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FooterHelpLink_OnAPageWithoutItsOwnSection_PointsAtTheTopOfHelp()
    {
        var (_, body) = await GetAsync("/Help/Index", "Participant");

        Assert.Contains("href=\"/Help/Index\" aria-label=\"View our help documentation\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Help_ShowsHomeThenHelpAsPlainTextInTheBreadcrumb()
    {
        var (_, body) = await GetAsync("/Help/Index", "Participant");

        Assert.Contains("<li class=\"govuk-breadcrumbs__list-item\" aria-current=\"page\">Help</li>", body, StringComparison.Ordinal);
    }
}
