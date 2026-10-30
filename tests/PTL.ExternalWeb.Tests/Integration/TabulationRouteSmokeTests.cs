using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Full-pipeline smoke tests for story 744651 (View Reports): the published tabulations list
// (legacy Tabulations.aspx), the read-only report (TabulationView.aspx) and the data-table
// Assessment Results screen (AssessmentResults.aspx).
public class TabulationRouteSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public TabulationRouteSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    private static PublishedTabulationSummaryResponse Summary(
        Guid tabulationId, string reference, string schemeCode, string schemeName, string tabulationName,
        DateTime date, int yearId, int monthId) =>
        new(tabulationId, reference, schemeCode, schemeName, tabulationName, date, yearId, monthId);

    private static TabulationReportResponse Report(Guid tabulationId, Guid? assessmentTestId = null) => new(
        tabulationId,
        IsPublished: true,
        DistributionTabulationId: tabulationId,
        Title: "FINAL REPORT for D1a/S1 BT: Brucella",
        TabulationName: "Summary",
        DistributionDateText: "Date Of Distribution: 01/05/2026",
        StandardTabulationTextHtml: "",
        CommentsHtml: "",
        TestConsultantCommentsHtml: "",
        TestConsultantCommentsTitle: "Comments :",
        ShowUkasLogo: false,
        Signatory: null,
        ParticipantOptions: [],
        SelectedParticipantId: Guid.Empty,
        ViewerOptions: [],
        SelectedViewerId: Guid.Empty,
        Tables:
        [
            new TabulationTableResponse(
                "Culture",
                [new TabulationGridRowResponse([new TabulationCellResponse("S001", 1, 1, "Cell", false)], IsHeader: false)],
                assessmentTestId)
        ],
        Ratings: null,
        Attachment: null,
        Print: new TabulationPrintResponse([], [], []),
        DistributionHeading: "D1a/S1 BT: Brucella");

    private HttpClient CreateClient(FakeDistributionApiClient apiClient) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(apiClient);
            })).CreateClient();

    private static async Task<Dictionary<string, string>> SignInAsync(HttpClient client, string roles, Guid? participantId = null, Guid? viewerId = null)
    {
        var cookies = new Dictionary<string, string>();
        var query = $"roles={Uri.EscapeDataString(roles)}";
        if (participantId is not null)
        {
            query += $"&participantId={participantId}";
        }

        if (viewerId is not null)
        {
            query += $"&viewerId={viewerId}";
        }

        var signInResponse = await client.GetAsync($"{PtlExternalWebTestFactory.TestSignInPath}?{query}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);
        return cookies;
    }

    [Fact]
    public async Task Index_ParticipantWithTabulations_RendersGridWithViewLinks()
    {
        var tabulationId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            Tabulations = new PublishedTabulationSummaryListResponse(
                [Summary(tabulationId, "D1a/00001", "BT", "Brucella", "Summary", new DateTime(2026, 5, 1), 2026, 5)])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, "Participant", participantId: Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Tabulation/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("D1a/00001", body);
        Assert.Contains("BT", body);
        Assert.Contains("Brucella", body);
        Assert.Contains($"/Tabulation/Report?publishedTabulationId={tabulationId}", body);
    }

    [Fact]
    public async Task Index_NoTabulations_RendersEmptyStateMessage()
    {
        var client = CreateClient(new FakeDistributionApiClient());
        var cookies = await SignInAsync(client, "Viewer", viewerId: Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Tabulation/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("There are no reports to display.", body);
    }

    [Fact]
    public async Task Report_ApiResolvesTheTabulation_RendersTheTable()
    {
        var tabulationId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { PublishedTabulation = Report(tabulationId) };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, "Participant", participantId: Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, $"/Tabulation/Report?publishedTabulationId={tabulationId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("S001", body);
        Assert.Contains("Generate PDF", body);

        // No participant/viewer switcher form - the ExternalWeb report is always the signed-in
        // user's own, unlike InternalWeb's staff-facing equivalent.
        var main = Regex.Match(body, "<main.*?</main>", RegexOptions.Singleline).Value;
        Assert.DoesNotContain("<select", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Report_ApiResolvesNothingForThisUser_RendersNotFound()
    {
        // Covers the Access Restrictions AC: ParticipantTabulationService only ever serves a
        // publishedTabulationId that is present in spgTabulations' own result for this SsoId, so a
        // tampered id that belongs to someone else's scheme also resolves to nothing.
        var apiClient = new FakeDistributionApiClient { PublishedTabulation = null };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, "Participant", participantId: Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, $"/Tabulation/Report?publishedTabulationId={Guid.NewGuid()}", cookies);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssessmentResults_SubmittedScores_RendersDataTableNotChart()
    {
        var tabulationId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            AssessmentResults = new AssessmentResultsResponse(
                IsAssessmentSubmitted: true,
                Titles: ["Assessment results for Culture test"],
                XAxisTitle: "Score",
                YAxisTitle: "Number of participants",
                Scores: [new AssessmentScoreFrequencyResponse(2, "Satisfactory", 5)],
                DistributionHeading: "D1a/S1 BT: Brucella")
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, "Participant", participantId: Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, $"/Tabulation/AssessmentResults?publishedTabulationId={tabulationId}&testId={testId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Satisfactory", body);
        Assert.Contains("govuk-table", body);
        Assert.DoesNotContain("ptl-assessment-chart", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_UnresolvedUser_IsForbidden()
    {
        var client = CreateClient(new FakeDistributionApiClient());
        var cookies = await SignInAsync(client, roles: "");

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Tabulation/Index", cookies);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HomeQuickLinkCard_ViewReports_NavigatesToRealTabulationControllerNotComingSoon()
    {
        var client = CreateClient(new FakeDistributionApiClient());
        var cookies = await SignInAsync(client, "Viewer", viewerId: Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/Tabulation/Index", body);
    }
}
