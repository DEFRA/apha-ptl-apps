using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/ResultsReceipt.cshtml (and the Dashboard's Results link) through
// the real view engine - legacy Internal User/ResultsReceipt.aspx parity.
public partial class ResultsReceiptRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private const string BaseQuery = "year=2026&monthId=5";
    private const string ViewDisabledTip = "The Results can not be viewed until the Samples for this Participant have been posted";
    private const string SamplesDisabledTip = "The Samples can not be submitted until the Samples for this Participant have been posted";

    private static readonly Guid SchemeA = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid SchemeB = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000b1");
    private static readonly Guid Posted = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid Unposted = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid NonAssessed = Guid.Parse("dddddddd-0000-0000-0000-000000000003");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public ResultsReceiptRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private static ResultsReceiptResponse Receipt() =>
        new(Guid.NewGuid(), 2026, 5,
        [
            new ResultsReceiptSchemeResponse(SchemeA, "D1a/S1: BT Brucella", RequiresAssessment: true, ProgressPercentage: 50,
            [
                new ResultsReceiptParticipantResponse(Posted, "L001: Lab One", new DateTime(2026, 6, 30), new DateTime(2026, 5, 3), new DateTime(2026, 5, 4),
                    ExtraDocumentsReturned: true, CanViewResults: true, HasUnaccountedSamples: true, ReturnedDate: new DateTime(2026, 5, 5)),
                new ResultsReceiptParticipantResponse(Unposted, "L002: Lab Two", new DateTime(2026, 7, 1), null, null,
                    ExtraDocumentsReturned: false, CanViewResults: false, HasUnaccountedSamples: false, ReturnedDate: null),
            ]),
            new ResultsReceiptSchemeResponse(SchemeB, "D2/S2: TB Tuberculosis", RequiresAssessment: false, ProgressPercentage: 0,
            [
                new ResultsReceiptParticipantResponse(NonAssessed, "L003: Lab Three", new DateTime(2026, 8, 2), null, null,
                    ExtraDocumentsReturned: false, CanViewResults: true, HasUnaccountedSamples: false, ReturnedDate: null),
            ]),
        ]);

    private async Task<string> GetHtmlAsync()
    {
        var response = await _factory.CreateClient().GetAsync($"/Distribution/ResultsReceipt?{BaseQuery}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Text(string fragment) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim());

    private static string? HiddenValue(string html, string name)
    {
        var tag = Regex.Matches(html, "<input[^>]*>").Select(m => m.Value)
            .FirstOrDefault(t => t.Contains($"name=\"{name}\"", StringComparison.Ordinal) && t.Contains("type=\"hidden\"", StringComparison.Ordinal));
        return tag is null ? null : Regex.Match(tag, "value=\"([^\"]*)\"").Groups[1].Value;
    }

    private static List<string> Rows(string html) =>
        [.. Regex.Matches(html, "<tr .*?</tr>", RegexOptions.Singleline).Select(m => m.Value)];

    private static string ParticipantRow(string html, string labCode) =>
        Rows(html).Single(r => r.Contains("ptl-results-receipt-participant-row", StringComparison.Ordinal) && r.Contains(labCode, StringComparison.Ordinal));

    private static List<string> Cells(string row) =>
        [.. Regex.Matches(row, "<t[dh] [^>]*>.*?</t[dh]>", RegexOptions.Singleline).Select(m => m.Value)];

    [Fact]
    public async Task Get_RendersLegacyTitleHeadingIntroAndColumnHeaders()
    {
        _apiClient.ResultsReceipt = Receipt();

        var html = await GetHtmlAsync();

        Assert.Contains("Results Receipt - May 2026", Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value, StringComparison.Ordinal);
        Assert.Equal("Results Receipt - May 2026", Text(Regex.Match(html, "<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value));
        Assert.Contains(
            "Each Scheme can be expanded to show the list of Participants. For each Participant clicking 'View' will show the results entered. The tick box is for registering the receipt of any extra documentation received.",
            WebUtility.HtmlDecode(html), StringComparison.Ordinal);

        var thead = Regex.Match(html, "<thead.*?</thead>", RegexOptions.Singleline).Value;
        var headers = Regex.Matches(thead, "<th [^>]*>(.*?)</th>", RegexOptions.Singleline).Select(m => Text(m.Groups[1].Value)).ToList();
        Assert.Equal(
            ["Participant", "Deadline Date", "First Edit", "Last Edit", "Extra Documents", "Results", "Submission Status", "Returned to Participant"],
            headers);
        Assert.Equal(1, Regex.Count(thead, "govuk-visually-hidden"));
    }

    [Fact]
    public async Task Get_SchemeSection_ShowsDescriptionAndSubmittedProportion()
    {
        _apiClient.ResultsReceipt = Receipt();

        var html = await GetHtmlAsync();

        Assert.Contains("data-module=\"govuk-accordion\"", html, StringComparison.Ordinal);
        Assert.Contains(">D1a/S1: BT Brucella</span>", html, StringComparison.Ordinal);
        Assert.Contains("1 of 2 Participants have submitted results (50%)", html, StringComparison.Ordinal);
        Assert.Matches("<progress class=\"ptl-progress-bar\" max=\"100\" value=\"50\"", html);
        Assert.Equal(2, Regex.Count(html, "govuk-accordion__section-content"));
    }

    [Fact]
    public async Task Get_PostedParticipantInAssessmentScheme_ShowsDatesLinksSuffixAndReturnedDate()
    {
        _apiClient.ResultsReceipt = Receipt();

        var row = ParticipantRow(await GetHtmlAsync(), "L001: Lab One");
        var cells = Cells(row);

        Assert.Equal("30/06/2026", Text(cells[1]));
        Assert.Equal("03/05/2026", Text(cells[2]));
        Assert.Equal("04/05/2026", Text(cells[3]));
        Assert.Contains("checked=\"checked\"", cells[4], StringComparison.Ordinal);
        Assert.Contains("href=", cells[5], StringComparison.Ordinal);
        Assert.Contains("/Distribution/ActualResults", cells[5], StringComparison.Ordinal);
        Assert.Contains($"participantId={Posted}", cells[5], StringComparison.Ordinal);
        Assert.Contains($"schemeId={SchemeA}", cells[5], StringComparison.Ordinal);
        Assert.DoesNotContain("ptl-link-disabled", cells[5], StringComparison.Ordinal);
        Assert.Contains("href=", cells[6], StringComparison.Ordinal);
        Assert.Contains("/Distribution/SampleReceipt", cells[6], StringComparison.Ordinal);
        Assert.Contains($"participantId={Posted}", cells[6], StringComparison.Ordinal);
        Assert.Contains($"schemeId={SchemeA}", cells[6], StringComparison.Ordinal);
        Assert.EndsWith("(Y)", Text(cells[6]), StringComparison.Ordinal);
        Assert.Equal("05/05/2026", Text(cells[7]));
        Assert.DoesNotContain("type=\"checkbox\"", cells[7], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_UnpostedParticipantWithNoResults_ShowsNoResultsDisabledLinksAndReturnedTickBox()
    {
        _apiClient.ResultsReceipt = Receipt();

        var row = ParticipantRow(await GetHtmlAsync(), "L002: Lab Two");
        var cells = Cells(row);

        Assert.Equal(7, cells.Count);
        Assert.Equal("No Results", Text(cells[2]));
        Assert.Contains("colspan=\"2\"", cells[2], StringComparison.Ordinal);
        Assert.Equal("01/07/2026", Text(cells[1]));
        Assert.DoesNotContain("checked=", cells[3], StringComparison.Ordinal);

        Assert.Contains("ptl-link-disabled", cells[4], StringComparison.Ordinal);
        Assert.Contains(ViewDisabledTip, Text(cells[4]), StringComparison.Ordinal);
        Assert.DoesNotContain("title=", cells[4], StringComparison.Ordinal);
        Assert.DoesNotContain("<a ", cells[4], StringComparison.Ordinal);

        Assert.Contains("ptl-link-disabled", cells[5], StringComparison.Ordinal);
        Assert.Contains(SamplesDisabledTip, Text(cells[5]), StringComparison.Ordinal);
        Assert.DoesNotContain("title=", cells[5], StringComparison.Ordinal);
        Assert.Contains("(N)", Text(cells[5]), StringComparison.Ordinal);

        Assert.Contains("type=\"checkbox\"", cells[6], StringComparison.Ordinal);
        Assert.Contains("name=\"Schemes[0].Participants[1].ReturnedChecked\"", cells[6], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_NonAssessmentScheme_HasNoSubmissionStatusOrReturnedToParticipant()
    {
        _apiClient.ResultsReceipt = Receipt();

        var row = ParticipantRow(await GetHtmlAsync(), "L003: Lab Three");
        var cells = Cells(row);

        // No First Edit date: "No Results" spans two cells, so the later columns shift left by one.
        Assert.Equal("No Results", Text(cells[2]));
        Assert.Contains("href=", cells[4], StringComparison.Ordinal);
        Assert.Equal(string.Empty, Text(cells[5]));
        Assert.Equal(string.Empty, Text(cells[6]));
        Assert.DoesNotContain("ReturnedChecked", row, StringComparison.Ordinal);
        Assert.DoesNotContain("(Y)", row, StringComparison.Ordinal);
        Assert.DoesNotContain("(N)", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_PostedFieldNames_UseIndexedSchemeAndParticipantPaths()
    {
        _apiClient.ResultsReceipt = Receipt();

        var html = await GetHtmlAsync();

        Assert.Equal(SchemeA.ToString(), HiddenValue(html, "Schemes[0].MonthlyDistributionSchemeId"));
        Assert.Equal(SchemeB.ToString(), HiddenValue(html, "Schemes[1].MonthlyDistributionSchemeId"));
        Assert.Equal(Posted.ToString(), HiddenValue(html, "Schemes[0].Participants[0].DistributionParticipantId"));
        Assert.Equal(NonAssessed.ToString(), HiddenValue(html, "Schemes[1].Participants[0].DistributionParticipantId"));
        Assert.Contains("name=\"Schemes[0].Participants[0].ExtraDocumentsReturned\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Schemes[0].Participants[1].ReturnedChecked\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_RendersSaveApplyCancelButtonsPostingTheCommand()
    {
        _apiClient.ResultsReceipt = Receipt();

        var html = await GetHtmlAsync();

        var buttons = Regex.Matches(html, "<button type=\"submit\" name=\"command\" value=\"(\\w+)\"[^>]*>(.*?)</button>", RegexOptions.Singleline)
            .Select(m => (Value: m.Groups[1].Value, Label: Text(m.Groups[2].Value)))
            .ToList();
        Assert.Equal([("save", "Save"), ("apply", "Apply"), ("cancel", "Cancel")], buttons);
    }

    [Fact]
    public async Task Get_NoSchemes_ShowsEmptyStateMessage()
    {
        _apiClient.ResultsReceipt = new ResultsReceiptResponse(Guid.NewGuid(), 2026, 5, []);

        var html = await GetHtmlAsync();

        Assert.Contains("There are no Schemes for this Distribution", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ptl-results-receipt-scheme-row", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_ApiReturnsNull_Returns404()
    {
        _apiClient.ResultsReceipt = null;

        var response = await _factory.CreateClient().GetAsync($"/Distribution/ResultsReceipt?{BaseQuery}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_SaveWithTicks_SendsThemToTheApiAndLeavesForTheDashboardWithASuccessBanner()
    {
        _apiClient.ResultsReceipt = Receipt();
        _apiClient.ResultsReceiptSaveResult = new ResultsReceiptSaveResult(true);
        var client = _factory.CreateClient();

        var response = await PostAsync(client, "save",
            ("Schemes[0].MonthlyDistributionSchemeId", SchemeA.ToString()),
            ("Schemes[0].Participants[0].DistributionParticipantId", Posted.ToString()),
            ("Schemes[0].Participants[0].ExtraDocumentsReturned", "false"),
            ("Schemes[0].Participants[1].DistributionParticipantId", Unposted.ToString()),
            ("Schemes[0].Participants[1].ExtraDocumentsReturned", "true"),
            ("Schemes[0].Participants[1].ExtraDocumentsReturned", "false"),
            ("Schemes[0].Participants[1].ReturnedChecked", "true"),
            ("Schemes[0].Participants[1].ReturnedChecked", "false"),
            ("Schemes[1].MonthlyDistributionSchemeId", SchemeB.ToString()),
            ("Schemes[1].Participants[0].DistributionParticipantId", NonAssessed.ToString()),
            ("Schemes[1].Participants[0].ExtraDocumentsReturned", "true"),
            ("Schemes[1].Participants[0].ExtraDocumentsReturned", "false"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Results Receipt updated successfully.", html, StringComparison.Ordinal);
        Assert.Contains("govuk-notification-banner--success", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"results-receipt-accordion\"", html, StringComparison.Ordinal);

        var request = Assert.Single(_apiClient.SavedResultsReceiptRequests);
        Assert.Equal(
            [
                new ResultsReceiptParticipantUpdateRequest(Posted, false, false),
                new ResultsReceiptParticipantUpdateRequest(Unposted, true, true),
                new ResultsReceiptParticipantUpdateRequest(NonAssessed, true, false),
            ],
            request.Updates);
    }

    [Fact]
    public async Task Post_Apply_StaysOnTheScreenWithASuccessBanner()
    {
        _apiClient.ResultsReceipt = Receipt();
        _apiClient.ResultsReceiptSaveResult = new ResultsReceiptSaveResult(true);

        var response = await PostAsync(_factory.CreateClient(), "apply");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Results Receipt updated successfully.", html, StringComparison.Ordinal);
        Assert.Contains("id=\"results-receipt-accordion\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_SaveFails_ReturnsToTheScreenWithAnErrorBanner()
    {
        _apiClient.ResultsReceipt = Receipt();
        _apiClient.ResultsReceiptSaveResult = null;

        var response = await PostAsync(_factory.CreateClient(), "save");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Results Receipt could not be saved. Please try again later.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-notification-banner--success", html, StringComparison.Ordinal);
        Assert.Contains("id=\"results-receipt-accordion\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_Cancel_DoesNotCallTheApi()
    {
        _apiClient.ResultsReceipt = Receipt();

        var response = await PostAsync(_factory.CreateClient(), "cancel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_apiClient.SavedResultsReceiptRequests);
        Assert.DoesNotContain("Results Receipt updated successfully.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dashboard_ResultsViewLink_PointsToTheResultsReceiptScreen()
    {
        _apiClient.Years = [new DistributionYearOptionResponse(2026, "2026/27")];
        _apiClient.Months = [new DistributionDashboardMonthResponse(2026, 5, "May", Guid.NewGuid(), false, 0, 0, 0, 0, 0)];

        var response = await _factory.CreateClient().GetAsync("/Distribution/Index?year=2026");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = Regex.Match(html, "<a [^>]*href=\"([^\"]+)\"[^>]*>View<span[^>]*> Results for May</span></a>");
        Assert.True(link.Success);
        var href = WebUtility.HtmlDecode(link.Groups[1].Value);
        Assert.Contains("/Distribution/ResultsReceipt", href, StringComparison.Ordinal);
        Assert.Contains("year=2026", href, StringComparison.Ordinal);
        Assert.Contains("monthId=5", href, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string command, params (string Name, string Value)[] fields)
    {
        var form = await client.GetAsync($"/Distribution/ResultsReceipt?{BaseQuery}");
        var token = AntiforgeryTokenPattern().Match(await form.Content.ReadAsStringAsync()).Groups[1].Value;

        var content = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token), new("command", command) };
        content.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));

        return await client.PostAsync($"/Distribution/ResultsReceipt?{BaseQuery}", new FormUrlEncodedContent(content));
    }
}
