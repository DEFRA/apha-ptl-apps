using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Tabulation.cshtml / PublishedTabulation.cshtml (via _TabulationReport.cshtml),
// PublishedTabulations.cshtml and AssessmentResults.cshtml through the real view engine - legacy
// Scheduling/Tabulation.aspx, PublishedTabulation.aspx, PublishedTabulations.aspx and AssessmentResults.aspx parity.
public class TabulationReportRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid TabulationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid PublishedId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid TestId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid AttachmentId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid ParticipantId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid ViewerId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");

    private static readonly string TabulationUrl = $"/Distribution/Tabulation?year=2026&monthId=5&tabulationId={TabulationId}";
    private static readonly string PublishedUrl = $"/Distribution/PublishedTabulation?year=2026&monthId=5&tabulationId={TabulationId}&publishedTabulationId={PublishedId}";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public TabulationReportRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private static TabulationCellResponse Cell(string text, int colSpan = 1, int rowSpan = 1, string kind = "Cell", bool nil = false) =>
        new(text, colSpan, rowSpan, kind, nil);

    private static TabulationGridRowResponse Row(bool header, params TabulationCellResponse[] cells) => new(cells, header);

    // Shaped like TabulationReportBuilder output for an assessed scheme.
    private static TabulationTableResponse AssessedTable() =>
        new("Clinical",
        [
            Row(true, Cell("Clinical", 1, 2, "Header1"), Cell("Method", 1, 2, "Header2"), Cell("S1", 5, 1, "Header1")),
            Row(true, Cell("Identity", kind: "Header2"), Cell("Clinical", kind: "Header2"), Cell("Pathology", kind: "Header2"), Cell("Assessment comment", kind: "Header2"), Cell("Overall Score", kind: "Header2")),
            Row(false, Cell("Intended", 1, 2), Cell("", kind: "Plain"), Cell("Brucella abortus", 5)),
            Row(false, Cell("ELISA"), Cell("Abortus"), Cell(""), Cell(""), Cell(""), Cell("")),
            Row(false, Cell("L001"), Cell("PCR"), Cell("Abortus"), Cell("Correct [3/5]"), Cell("", nil: true), Cell("Fine"), Cell("Good [4/5]")),
            Row(false, Cell("L002"), Cell("No return", nil: true), Cell("Not Submitted", 5)),
        ],
        TestId);

    // A non-assessed method-only test: its second heading row is empty.
    private static TabulationTableResponse MethodsTable() =>
        new("Methods",
        [
            Row(true, Cell("Methods", 1, 2, "Header1"), Cell("Method", 1, 2, "Header2")),
            Row(true),
            Row(false, Cell("Intended", 1, 2), Cell("", kind: "Plain"), Cell("Desc")),
            Row(false, Cell("ELISA")),
            Row(false, Cell("L001"), Cell("Late", 3, nil: true)),
            Row(false),
            Row(false, Cell("Note"), Cell("Sub heading", kind: "Header2")),
        ],
        null);

    private static TabulationTableResponse RatingsTable() =>
        new("QAU Ratings",
        [
            Row(true, Cell("QAU Ratings", kind: "Header1"), Cell("S1", kind: "Header2")),
            Row(false, Cell("L001"), Cell("2")),
            Row(false, Cell("Mean score of all laboratories: "), Cell("2.0")),
        ],
        null);

    private static TabulationPrintResponse Print(string labCode = "L001") =>
        new(
            [new(labCode, true, "")],
            [new("Clinical", [new("Method", "ELISA", ["PCR"])], [new("S1", "Brucella abortus", [new("Identity", "Abortus", [new("Abortus", 1)])])])],
            [new("S1", ["2"], "2.0", "100.0%", "1", "1")]);

    private static TabulationReportResponse Report(
        bool published = false,
        IReadOnlyList<TabulationFilterOptionResponse>? participants = null,
        IReadOnlyList<TabulationFilterOptionResponse>? viewers = null,
        TabulationAttachmentResponse? attachment = null,
        string labCode = "L001",
        bool ukas = false,
        string comments = "",
        string consultantComments = "",
        string standardText = "") =>
        new(
            published ? PublishedId : TabulationId,
            published,
            TabulationId,
            "FINAL REPORT for D1a/S1 BT: Brucella",
            "Summary",
            published ? "Distribution Date: 03/05/2026" : "Date Of Distribution: 03/05/2026",
            standardText,
            comments,
            consultantComments,
            "Comments provided by Dr A, QAU:",
            ukas,
            published
                ? new TabulationSignatoryResponse("Version: 01/04/2025", "Dr Signatory", ["Animal and Plant Health Agency", "Quality Assurance Unit", "UK"], "Issued: 10/06/2026")
                : null,
            participants ?? [],
            participants is { Count: > 1 } ? participants[1].Value : Guid.Empty,
            viewers ?? [],
            viewers is { Count: > 1 } ? viewers[1].Value : Guid.Empty,
            [AssessedTable(), MethodsTable()],
            RatingsTable(),
            attachment,
            Print(labCode));

    private async Task<string> GetHtmlAsync(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Text(string fragment) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim());

    private static string Heading(string html, int level) =>
        Text(Regex.Match(html, $"<h{level}[^>]*>(.*?)</h{level}>", RegexOptions.Singleline).Groups[1].Value);

    private static string Title(string html) => Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value;

    private static string HrefOf(string html, string linkPattern)
    {
        var match = Regex.Match(html, $"<a [^>]*href=\"([^\"]+)\"[^>]*>{linkPattern}");
        Assert.True(match.Success, $"Link '{linkPattern}' not found");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static (string Path, System.Collections.Specialized.NameValueCollection Query) Parse(string href)
    {
        var index = href.IndexOf('?', StringComparison.Ordinal);
        return index < 0 ? (href, new System.Collections.Specialized.NameValueCollection()) : (href[..index], HttpUtility.ParseQueryString(href[(index + 1)..]));
    }

    private static string TableHtml(string html, string caption)
    {
        var match = Regex.Match(html, $"<table class=\"TabulationTable\">\\s*<caption class=\"govuk-visually-hidden\">{Regex.Escape(caption)}</caption>(.*?)</table>", RegexOptions.Singleline);
        Assert.True(match.Success, $"Table '{caption}' not found");
        return match.Groups[1].Value;
    }

    private static List<string> TableRows(string table) =>
        [.. Regex.Matches(table, "<tr>(.*?)</tr>", RegexOptions.Singleline).Select(m => Regex.Replace(m.Groups[1].Value, @">\s+<", "><").Trim())];

    [Fact]
    public async Task Tabulation_Missing_ReturnsNotFound()
    {
        var response = await _factory.CreateClient().GetAsync(TabulationUrl);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Tabulation_RendersTitleHeadingsAndBackToTabulations()
    {
        _apiClient.TabulationReport = Report();

        var html = await GetHtmlAsync(TabulationUrl);

        Assert.Contains("Tabulation", Title(html), StringComparison.Ordinal);
        Assert.Equal("FINAL REPORT for D1a/S1 BT: Brucella", Heading(html, 1));
        Assert.Equal("Summary", Heading(html, 2));
        // The draft only shows the distribution date in the print header.
        Assert.DoesNotContain("<p class=\"govuk-body\">Date Of Distribution", html, StringComparison.Ordinal);
        Assert.Contains("tabulationtitle-alignright\">Date Of Distribution: 03/05/2026</div>", html, StringComparison.Ordinal);

        var (path, query) = Parse(HrefOf(html, "Back</a>"));
        Assert.Equal("/Distribution/Tabulations", path);
        Assert.Equal(("2026", "5", null), (query["year"], query["monthId"], query["tabulationId"]));
        Assert.Matches("<a class=\"govuk-button govuk-button--secondary\"[^>]*href=\"/Distribution/Tabulations\\?year=2026&amp;monthId=5\">Back</a>", html);
        Assert.Contains("data-ptl-print>Print</button>", html, StringComparison.Ordinal);
        Assert.Contains("/js/tabulation-print.js", html, StringComparison.Ordinal);
        Assert.Contains("/css/tabulation.css", html, StringComparison.Ordinal);
        Assert.DoesNotContain("signatureTable", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_NoFilterOptions_RendersNoFilterForm()
    {
        _apiClient.TabulationReport = Report();

        var html = await GetHtmlAsync(TabulationUrl);

        Assert.DoesNotContain("action=\"/Distribution/Tabulation\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Select Participant:", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Select Viewer:", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_ParticipantFilter_RendersGetFormWithSelectedParticipant()
    {
        _apiClient.TabulationReport = Report(participants: [new(Guid.Empty, "All Participants"), new(ParticipantId, "L001")]);

        var html = await GetHtmlAsync(TabulationUrl + $"&participantId={ParticipantId}");

        Assert.Equal([(TabulationId, ParticipantId, Guid.Empty)], _apiClient.RequestedTabulations);
        var form = Regex.Match(html, "<form method=\"get\"[^>]*action=\"/Distribution/Tabulation\"[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.True(form.Success);
        var body = form.Groups[1].Value;
        Assert.Contains("<input type=\"hidden\" name=\"year\" value=\"2026\" />", body, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"monthId\" value=\"5\" />", body, StringComparison.Ordinal);
        Assert.Contains($"<input type=\"hidden\" name=\"tabulationId\" value=\"{TabulationId}\" />", body, StringComparison.Ordinal);
        Assert.DoesNotContain("publishedTabulationId", body, StringComparison.Ordinal);
        Assert.Contains("<label class=\"govuk-label\" for=\"participantId\">Select Participant:</label>", body, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{Guid.Empty}\">All Participants</option>", body, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{ParticipantId}\" selected=\"selected\">L001</option>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("viewerId", body, StringComparison.Ordinal);
        Assert.Contains(">View</button>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_RendersTheLegacyGridWithSpansScopesAndNilReturnCells()
    {
        _apiClient.TabulationReport = Report();

        var html = await GetHtmlAsync(TabulationUrl);

        Assert.Contains("role=\"region\" aria-label=\"Clinical\"", html, StringComparison.Ordinal);
        var table = TableHtml(html, "Clinical");
        var head = Regex.Match(table, "<thead>(.*?)</thead>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(2, Regex.Matches(head, "<tr>").Count);
        var rows = TableRows(table);
        Assert.Equal(6, rows.Count);
        Assert.Equal(
            "<th scope=\"col\" class=\"TabulationHeader1\" rowspan=\"2\">Clinical</th><th scope=\"col\" class=\"TabulationHeader2\" rowspan=\"2\">Method</th><th scope=\"col\" class=\"TabulationHeader1\" colspan=\"5\">S1</th>",
            rows[0]);
        Assert.Equal(5, Regex.Matches(rows[1], "<th scope=\"col\" class=\"TabulationHeader2\">").Count);
        Assert.Equal(
            "<th scope=\"row\" class=\"TabulationCell\" rowspan=\"2\">Intended</th><td>&nbsp;</td><td class=\"TabulationCell\" colspan=\"5\">Brucella abortus</td>",
            rows[2]);
        // Column 0 of the second intended row is still covered by the Intended rowspan.
        Assert.StartsWith("<td class=\"TabulationCell\">ELISA</td>", rows[3], StringComparison.Ordinal);
        Assert.StartsWith("<th scope=\"row\" class=\"TabulationCell\">L001</th>", rows[4], StringComparison.Ordinal);
        Assert.Contains("<td class=\"TabulationCell NilReturn\">&nbsp;</td>", rows[4], StringComparison.Ordinal);
        Assert.Contains("<td class=\"TabulationCell\">Good [4/5]</td>", rows[4], StringComparison.Ordinal);
        Assert.Equal(
            "<th scope=\"row\" class=\"TabulationCell\">L002</th><td class=\"TabulationCell NilReturn\">No return</td><td class=\"TabulationCell\" colspan=\"5\">Not Submitted</td>",
            rows[5]);
    }

    [Fact]
    public async Task Tabulation_EmptyHeadingRowsAndHeadingCellsInBodyRowsRenderAsLegacy()
    {
        _apiClient.TabulationReport = Report();

        var html = await GetHtmlAsync(TabulationUrl);

        var rows = TableRows(TableHtml(html, "Methods"));
        Assert.Equal(7, rows.Count);
        Assert.Equal(string.Empty, rows[1]);
        Assert.Equal("<th scope=\"row\" class=\"TabulationCell\">L001</th><td class=\"TabulationCell NilReturn\" colspan=\"3\">Late</td>", rows[4]);
        Assert.Equal(string.Empty, rows[5]);
        Assert.Equal("<th scope=\"row\" class=\"TabulationCell\">Note</th><th class=\"TabulationHeader2\">Sub heading</th>", rows[6]);
        Assert.DoesNotContain("View assessment results<span class=\"govuk-visually-hidden\"> for Methods", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_RendersTheRatingsTable()
    {
        _apiClient.TabulationReport = Report();

        var html = await GetHtmlAsync(TabulationUrl);

        var rows = TableRows(TableHtml(html, "QAU Ratings"));
        Assert.Equal("<th scope=\"col\" class=\"TabulationHeader1\">QAU Ratings</th><th scope=\"col\" class=\"TabulationHeader2\">S1</th>", rows[0]);
        Assert.Equal("<th scope=\"row\" class=\"TabulationCell\">L001</th><td class=\"TabulationCell\">2</td>", rows[1]);
        Assert.Equal("<th scope=\"row\" class=\"TabulationCell\">Mean score of all laboratories: </th><td class=\"TabulationCell\">2.0</td>", rows[2]);
    }

    [Fact]
    public async Task Tabulation_AssessmentLinkGoesToTheDraftAssessmentResults()
    {
        _apiClient.TabulationReport = Report();

        var html = await GetHtmlAsync(TabulationUrl);

        var (path, query) = Parse(HrefOf(html, "View assessment results<span class=\"govuk-visually-hidden\"> for Clinical</span>"));
        Assert.Equal("/Distribution/TabulationAssessmentResults", path);
        Assert.Equal(("2026", "5", TabulationId.ToString(), TestId.ToString(), null),
            (query["year"], query["monthId"], query["tabulationId"], query["testId"], query["publishedTabulationId"]));
    }

    [Fact]
    public async Task Tabulation_AttachmentLinksToTheDraftDownload()
    {
        _apiClient.TabulationReport = Report(attachment: new(AttachmentId, "notes.pdf"));

        var html = await GetHtmlAsync(TabulationUrl);

        Assert.Contains("<strong>Supporting Document: </strong>", html, StringComparison.Ordinal);
        var (path, query) = Parse(HrefOf(html, "View Supporting Document</a>"));
        Assert.Equal("/Distribution/TabulationAttachment", path);
        Assert.Equal((TabulationId.ToString(), AttachmentId.ToString()), (query["tabulationId"], query["attachmentId"]));
    }

    [Fact]
    public async Task Tabulation_NoAttachmentOrUkasOrComments_RendersNoneOfThosePanels()
    {
        _apiClient.TabulationReport = Report();

        var html = await GetHtmlAsync(TabulationUrl);

        Assert.DoesNotContain("Supporting Document", html, StringComparison.Ordinal);
        Assert.DoesNotContain("UKAS accredited", html, StringComparison.Ordinal);
        Assert.DoesNotContain("divTestConsultantComments", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_RendersCommentsPanelsAndUkasLogo()
    {
        _apiClient.TabulationReport = Report(
            ukas: true, standardText: "<p>Standard text</p>", comments: "<p>Sample <b>comments</b></p>", consultantComments: "<p>Consultant note</p>");

        var html = await GetHtmlAsync(TabulationUrl);

        var comments = Regex.Match(html, "<div id=\"commentsDiv\">(.*)<div class=\"govuk-button-group noprint\">", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(3, Regex.Matches(comments, "<div class=\"divTestConsultantComments\">").Count);
        Assert.Contains("<p>Standard text</p>", comments, StringComparison.Ordinal);
        Assert.Contains("<p>Sample <b>comments</b></p>", comments, StringComparison.Ordinal);
        Assert.Contains("<p class=\"commentsTitle\">Comments provided by Dr A, QAU:</p>", comments, StringComparison.Ordinal);
        Assert.Contains("<p>Consultant note</p>", comments, StringComparison.Ordinal);
        Assert.Contains("alt=\"UKAS accredited\"", comments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_PrintDataIsCamelCaseJsonWithScriptSafeEscaping()
    {
        _apiClient.TabulationReport = Report(labCode: "</script><script>alert(1)</script>");

        var html = await GetHtmlAsync(TabulationUrl);

        Assert.Contains("<div id=\"tabulation-print-tables\"></div>", html, StringComparison.Ordinal);
        var script = Regex.Match(html, "<script type=\"application/json\" id=\"tabulation-print-data\">(.*?)</script>", RegexOptions.Singleline);
        Assert.True(script.Success);
        Assert.DoesNotContain("<script>alert(1)", html, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(script.Groups[1].Value);
        var root = json.RootElement;
        Assert.Equal("</script><script>alert(1)</script>", root.GetProperty("participants")[0].GetProperty("labCode").GetString());
        Assert.True(root.GetProperty("participants")[0].GetProperty("hasResults").GetBoolean());
        var test = root.GetProperty("tests")[0];
        Assert.Equal("Clinical", test.GetProperty("name").GetString());
        Assert.Equal("ELISA", test.GetProperty("methodColumns")[0].GetProperty("intendedResult").GetString());
        var result = test.GetProperty("sampleColumns")[0].GetProperty("resultColumns")[0].GetProperty("results")[0];
        Assert.Equal(("Abortus", 1), (result.GetProperty("result").GetString(), result.GetProperty("span").GetInt32()));
        var rating = root.GetProperty("ratingColumns")[0];
        Assert.Equal(("2.0", "100.0%", "1", "1"),
            (rating.GetProperty("sampleMean").GetString(), rating.GetProperty("samplePassRate").GetString(),
             rating.GetProperty("numberOfParticipants").GetString(), rating.GetProperty("numberReportingIntendedResult").GetString()));
    }

    [Fact]
    public async Task PublishedTabulation_Missing_ReturnsNotFound()
    {
        var response = await _factory.CreateClient().GetAsync(PublishedUrl);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PublishedTabulation_RendersDateSignatoryAndBackToIssuedTabulations()
    {
        _apiClient.PublishedTabulationReport = Report(published: true);

        var html = await GetHtmlAsync(PublishedUrl);

        Assert.Contains("Issued Tabulation", Title(html), StringComparison.Ordinal);
        Assert.Equal("FINAL REPORT for D1a/S1 BT: Brucella", Heading(html, 1));
        Assert.Contains("<p class=\"govuk-body\">Distribution Date: 03/05/2026</p>", html, StringComparison.Ordinal);
        Assert.Equal([(PublishedId, Guid.Empty, Guid.Empty)], _apiClient.RequestedTabulations);

        var signature = Regex.Match(html, "<div class=\"signatureTable\">(.*?)</div>\\s*</div>\\s*</div>", RegexOptions.Singleline).Value;
        Assert.Contains("<div class=\"cellVersionDate\">Version: 01/04/2025</div>", signature, StringComparison.Ordinal);
        Assert.Contains("<div>Dr Signatory</div>", signature, StringComparison.Ordinal);
        Assert.Equal(
            ["Animal and Plant Health Agency", "Quality Assurance Unit", "UK", "Issued: 10/06/2026"],
            Regex.Matches(signature, "<div class=\"cellTabulationSignature2\">(.*?)</div>").Select(m => m.Groups[1].Value));

        var (path, query) = Parse(HrefOf(html, "Back</a>"));
        Assert.Equal("/Distribution/PublishedTabulations", path);
        Assert.Equal(("2026", "5", TabulationId.ToString()), (query["year"], query["monthId"], query["tabulationId"]));
    }

    [Fact]
    public async Task PublishedTabulation_ViewerFilter_RendersGetFormBackToTheIssuedTabulation()
    {
        _apiClient.PublishedTabulationReport = Report(published: true, viewers: [new(Guid.Empty, "Normal View"), new(ViewerId, "Viewer One")]);

        var html = await GetHtmlAsync(PublishedUrl + $"&viewerId={ViewerId}");

        Assert.Equal([(PublishedId, Guid.Empty, ViewerId)], _apiClient.RequestedTabulations);
        var form = Regex.Match(html, "<form method=\"get\"[^>]*action=\"/Distribution/PublishedTabulation\"[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.True(form.Success);
        var body = form.Groups[1].Value;
        Assert.Contains($"<input type=\"hidden\" name=\"tabulationId\" value=\"{TabulationId}\" />", body, StringComparison.Ordinal);
        Assert.Contains($"<input type=\"hidden\" name=\"publishedTabulationId\" value=\"{PublishedId}\" />", body, StringComparison.Ordinal);
        Assert.Contains("<label class=\"govuk-label\" for=\"viewerId\">Select Viewer:</label>", body, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{Guid.Empty}\">Normal View</option>", body, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{ViewerId}\" selected=\"selected\">Viewer One</option>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Select Participant:", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishedTabulation_AssessmentAndAttachmentLinksUseTheIssuedRoutes()
    {
        _apiClient.PublishedTabulationReport = Report(published: true, attachment: new(AttachmentId, "issued.pdf"));

        var html = await GetHtmlAsync(PublishedUrl);

        var (path, query) = Parse(HrefOf(html, "View assessment results"));
        Assert.Equal("/Distribution/PublishedAssessmentResults", path);
        Assert.Equal(("2026", "5", TabulationId.ToString(), TestId.ToString(), PublishedId.ToString()),
            (query["year"], query["monthId"], query["tabulationId"], query["testId"], query["publishedTabulationId"]));

        var (attachmentPath, attachmentQuery) = Parse(HrefOf(html, "View Supporting Document</a>"));
        Assert.Equal("/Distribution/PublishedTabulationAttachment", attachmentPath);
        Assert.Equal((PublishedId.ToString(), AttachmentId.ToString()), (attachmentQuery["publishedTabulationId"], attachmentQuery["attachmentId"]));
    }

    [Fact]
    public async Task PublishedTabulations_Missing_ReturnsNotFound()
    {
        var response = await _factory.CreateClient().GetAsync($"/Distribution/PublishedTabulations?year=2026&monthId=5&tabulationId={TabulationId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PublishedTabulations_NoIssuedVersions_ShowsLegacyEmptyText()
    {
        _apiClient.PublishedTabulations = new PublishedTabulationsResponse(TabulationId, "D1a/S1 BT: Brucella", "Summary", []);

        var html = await GetHtmlAsync($"/Distribution/PublishedTabulations?year=2026&monthId=5&tabulationId={TabulationId}");

        Assert.Contains("Issued Tabulations", Title(html), StringComparison.Ordinal);
        Assert.Equal("D1a/S1 BT: Brucella", Heading(html, 1));
        Assert.Equal("Summary", Heading(html, 2));
        Assert.Contains("The table below lists all of the issued versions of this Tabulation.", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"govuk-body\">There are no issued versions of this Tabulation.</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Date Issued", html, StringComparison.Ordinal);
        Assert.Matches("<a class=\"govuk-back-link\" href=\"/Distribution/Tabulations\\?year=2026&amp;monthId=5\">Back</a>", html);
        Assert.Matches("<a class=\"govuk-button govuk-button--secondary\"[^>]*href=\"/Distribution/Tabulations\\?year=2026&amp;monthId=5\">Back</a>", html);
    }

    [Fact]
    public async Task PublishedTabulations_ListsEachIssuedVersionWithItsViewLink()
    {
        var second = Guid.NewGuid();
        _apiClient.PublishedTabulations = new PublishedTabulationsResponse(TabulationId, "D1a/S1 BT: Brucella", "Summary",
        [
            new(PublishedId, new DateTime(2026, 6, 10, 14, 30, 5), "Dr A"),
            new(second, new DateTime(2026, 6, 11, 9, 0, 0), "Dr B"),
        ]);

        var html = await GetHtmlAsync($"/Distribution/PublishedTabulations?year=2026&monthId=5&tabulationId={TabulationId}");

        Assert.DoesNotContain("There are no issued versions", html, StringComparison.Ordinal);
        Assert.Contains("<caption class=\"govuk-table__caption govuk-visually-hidden\">Issued versions of Summary</caption>", html, StringComparison.Ordinal);
        Assert.Equal(
            ["Date Issued", "Signatory", "View"],
            Regex.Matches(html, "<th scope=\"col\" class=\"govuk-table__header\">(.*?)</th>").Select(m => m.Groups[1].Value));
        var body = Regex.Match(html, "<tbody class=\"govuk-table__body\">(.*?)</tbody>", RegexOptions.Singleline).Groups[1].Value;
        var rows = Regex.Matches(body, "<tr class=\"govuk-table__row\">(.*?)</tr>", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("10/06/2026 14:30:05 Dr A View issued tabulation of 10/06/2026 14:30:05", Text(rows[0]));
        var (path, query) = Parse(HrefOf(rows[0], "View"));
        Assert.Equal("/Distribution/PublishedTabulation", path);
        Assert.Equal(("2026", "5", TabulationId.ToString(), PublishedId.ToString()),
            (query["year"], query["monthId"], query["tabulationId"], query["publishedTabulationId"]));
        Assert.Equal(second.ToString(), Parse(HrefOf(rows[1], "View")).Query["publishedTabulationId"]);
    }

    private static AssessmentResultsResponse Results(bool submitted, IReadOnlyList<string>? titles = null, IReadOnlyList<AssessmentScoreFrequencyResponse>? scores = null) =>
        new(
            submitted,
            titles ?? ["Bar chart shows the frequency of assessment scores", "Scheme: Brucella", " ", "Average score: 3.5"],
            "Assessment Score",
            "Frequency",
            scores ?? [new(1, "1 Fail", 3), new(5, "5 Excellent", 25), new(0, "Not Submitted", 0)]);

    private static string AssessmentUrl(bool issued) => issued
        ? $"/Distribution/PublishedAssessmentResults?year=2026&monthId=5&tabulationId={TabulationId}&publishedTabulationId={PublishedId}&testId={TestId}"
        : $"/Distribution/TabulationAssessmentResults?year=2026&monthId=5&tabulationId={TabulationId}&testId={TestId}";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AssessmentResults_Missing_ReturnsNotFound(bool issued)
    {
        var response = await _factory.CreateClient().GetAsync(AssessmentUrl(issued));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssessmentResults_NotSubmitted_ShowsOnlyTheBackButtons()
    {
        _apiClient.AssessmentResults = Results(submitted: false);

        var html = await GetHtmlAsync(AssessmentUrl(issued: false));

        Assert.Contains("Assessment Results", Title(html), StringComparison.Ordinal);
        Assert.Equal("Assessment Results", Heading(html, 1));
        Assert.DoesNotContain("<svg class=\"ptl-assessment-chart\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("(data table)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Bar chart shows", html, StringComparison.Ordinal);
        var backs = Regex.Matches(html, "<a class=\"govuk-[^\"]*\"[^>]*href=\"([^\"]+)\">Back</a>").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
        Assert.Equal(2, backs.Count);
        Assert.All(backs, href =>
        {
            var (path, query) = Parse(href);
            Assert.Equal("/Distribution/Tabulation", path);
            Assert.Equal(("2026", "5", TabulationId.ToString(), null), (query["year"], query["monthId"], query["tabulationId"], query["publishedTabulationId"]));
        });
        Assert.Equal([(TabulationId, TestId, false)], _apiClient.RequestedAssessmentResults);
    }

    [Fact]
    public async Task AssessmentResults_Submitted_RendersTitlesChartAndDataTable()
    {
        _apiClient.AssessmentResults = Results(submitted: true);

        var html = await GetHtmlAsync(AssessmentUrl(issued: false));

        Assert.Equal("Bar chart shows the frequency of assessment scores", Heading(html, 2));
        var list = Regex.Match(html, "<ul class=\"govuk-list\">(.*?)</ul>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(["Scheme: Brucella", "Average score: 3.5"], Regex.Matches(list, "<li>(.*?)</li>").Select(m => m.Groups[1].Value));

        var svg = Regex.Match(html, "<svg class=\"ptl-assessment-chart\".*?</svg>", RegexOptions.Singleline).Value;
        Assert.Contains("<title id=\"assessment-chart-title\">Bar chart shows the frequency of assessment scores</title>", svg, StringComparison.Ordinal);
        Assert.Contains("Frequency by Assessment Score.", svg, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Matches(svg, "<rect ").Count);
        Assert.Contains(">5 Excellent</text>", svg, StringComparison.Ordinal);
        // 25 at most: ticks every 3 up to 27.
        Assert.Contains(">27</text>", svg, StringComparison.Ordinal);

        Assert.Contains("<caption class=\"govuk-table__caption govuk-table__caption--s\">Bar chart shows the frequency of assessment scores (data table)</caption>", html, StringComparison.Ordinal);
        var body = Regex.Match(html, "<tbody class=\"govuk-table__body\">(.*?)</tbody>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(
            ["1 Fail 3", "5 Excellent 25", "Not Submitted 0"],
            Regex.Matches(body, "<tr class=\"govuk-table__row\">(.*?)</tr>", RegexOptions.Singleline).Select(m => Text(m.Groups[1].Value)));
        Assert.Contains("<th scope=\"row\" class=\"govuk-table__header\">1 Fail</th>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssessmentResults_SubmittedWithoutTitlesOrScores_StillDrawsTheAxes()
    {
        _apiClient.AssessmentResults = Results(submitted: true, titles: [], scores: []);

        var html = await GetHtmlAsync(AssessmentUrl(issued: false));

        Assert.Contains("<svg class=\"ptl-assessment-chart\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<ul class=\"govuk-list\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<rect ", html, StringComparison.Ordinal);
        Assert.Contains(">1</text>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssuedAssessmentResults_UsesTheIssuedTitleAndReturnsToTheIssuedTabulation()
    {
        _apiClient.AssessmentResults = Results(submitted: true);

        var html = await GetHtmlAsync(AssessmentUrl(issued: true));

        Assert.Contains("Issued Assessment Results", Title(html), StringComparison.Ordinal);
        Assert.Equal("Issued Assessment Results", Heading(html, 1));
        var (path, query) = Parse(HrefOf(html, "Back</a>"));
        Assert.Equal("/Distribution/PublishedTabulation", path);
        Assert.Equal(("2026", "5", TabulationId.ToString(), PublishedId.ToString()),
            (query["year"], query["monthId"], query["tabulationId"], query["publishedTabulationId"]));
        Assert.Equal([(PublishedId, TestId, true)], _apiClient.RequestedAssessmentResults);
    }

    [Theory]
    [InlineData("TabulationAttachment", "tabulationId")]
    [InlineData("PublishedTabulationAttachment", "publishedTabulationId")]
    public async Task Attachment_Missing_ReturnsNotFound(string action, string idKey)
    {
        var response = await _factory.CreateClient().GetAsync($"/Distribution/{action}?{idKey}={TabulationId}&attachmentId={AttachmentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("TabulationAttachment", "tabulationId")]
    [InlineData("PublishedTabulationAttachment", "publishedTabulationId")]
    public async Task Attachment_PassesTheApiHeadersThrough(string action, string idKey)
    {
        _apiClient.TabulationAttachment = new TabulationAttachmentFile([37, 80, 68, 70], "application/pdf", "inline; filename=notes.pdf");

        var response = await _factory.CreateClient().GetAsync($"/Distribution/{action}?{idKey}={TabulationId}&attachmentId={AttachmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("inline; filename=notes.pdf", response.Content.Headers.ContentDisposition!.ToString());
        Assert.Equal([37, 80, 68, 70], await response.Content.ReadAsByteArrayAsync());
    }
}
