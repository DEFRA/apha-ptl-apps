using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/Packaging.cshtml and PackagingLabels.cshtml through the real
// view engine (legacy Internal User/PackagingSheet.aspx parity), so runtime view errors and the
// legacy column/row/checkbox rules are exercised as markup, not just as controller return values.
public partial class PackagingRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private const string PreparationIncomplete = "The preparation for this scheme is not complete";
    private const string BlurbText = "Select a packaging option and, if needed, a week. Then choose View to show the sample packaging list and any special packing instructions.";
    private const string BaseQuery = "year=2026&monthId=4";

    private static readonly DateTime Apr10 = new(2026, 4, 10);
    private static readonly DateTime Apr11 = new(2026, 4, 11);
    private static readonly DateTime Apr12 = new(2026, 4, 12);
    private static readonly DateTime Apr13 = new(2026, 4, 13);

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public PackagingRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    // ---- data builders ------------------------------------------------------------------------

    private static Guid Id(int n) => Guid.Parse($"aaaaaaaa-0000-0000-0000-{n:D12}");

    private static PackagingParticipantResponse Part(
        int n, string description, DateTime? packaged = null, DateTime? posted = null, string permit = "N/A") =>
        new(Id(n), description, packaged, posted, permit);

    private static PackagingSchemeResponse Scheme(
        int n,
        string description,
        bool prep = true,
        string packingInfo = "",
        string customerPackingInfo = "",
        string psLevelPackingInfo = "",
        string permit = "",
        int? ownRow = null,
        DateTime? packaged = null,
        DateTime? posted = null,
        int packedTotal = 0,
        int postedTotal = 0,
        params PackagingParticipantResponse[] participants) =>
        new(Id(1000 + n), description, prep, packingInfo, customerPackingInfo, psLevelPackingInfo, permit,
            ownRow is { } row ? Id(row) : null, packaged, posted, packedTotal, postedTotal, participants);

    private static PackagingBlockResponse Block(
        string printKey,
        DateTime date,
        string heading,
        string packingInfo = "",
        bool due = false,
        bool showAllPacked = false,
        bool showAllPosted = false,
        params PackagingSchemeResponse[] schemes) =>
        new(printKey, date, heading, packingInfo, due, showAllPacked, showAllPosted, schemes);

    private static PackagingSheetResponse Sheet(int mode, int week, params PackagingBlockResponse[] blocks) =>
        new(mode, week, 2026, 4, blocks);

    // ---- HTTP helpers -------------------------------------------------------------------------

    private async Task<string> GetHtmlAsync(string query)
    {
        var response = await _factory.CreateClient().GetAsync($"/Distribution/Packaging?{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, params (string Name, string Value)[] fields)
    {
        var form = await client.GetAsync($"/Distribution/Packaging?{BaseQuery}");
        var token = AntiforgeryTokenPattern().Match(await form.Content.ReadAsStringAsync()).Groups[1].Value;

        var content = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        content.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));

        return await client.PostAsync($"/Distribution/Packaging?{BaseQuery}", new FormUrlEncodedContent(content));
    }

    private async Task<string> PostHtmlAsync(params (string Name, string Value)[] fields)
    {
        var response = await PostAsync(_factory.CreateClient(), fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    // ---- HTML helpers (stable markers only, never whitespace) ----------------------------------

    private static string Text(string fragment) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim());

    private static string? Attr(string tag, string name)
    {
        var match = Regex.Match(tag, $@"\s{Regex.Escape(name)}=""([^""]*)""");
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    private static List<string> Inputs(string html, string name) =>
        [.. Regex.Matches(html, "<input[^>]*>").Select(m => m.Value).Where(t => Attr(t, "name") == name)];

    private static List<string> Checkboxes(string html, string name) =>
        [.. Inputs(html, name).Where(t => Attr(t, "type") == "checkbox")];

    private static List<string> HiddenInputs(string html, string name) =>
        [.. Inputs(html, name).Where(t => Attr(t, "type") == "hidden")];

    private static List<string> Headers(string html)
    {
        var thead = Regex.Match(html, "<thead.*?</thead>", RegexOptions.Singleline).Value;
        return [.. Regex.Matches(thead, "<th [^>]*>(.*?)</th>", RegexOptions.Singleline).Select(m =>
        {
            var inner = m.Groups[1].Value;
            var select = inner.IndexOf("<div", StringComparison.Ordinal);
            return Text(select >= 0 ? inner[..select] : inner);
        })];
    }

    private static List<string> Cells(string html, string cssClass) =>
        [.. Regex.Matches(html, $"<(?:td|th) (?:scope=\"[a-z]+\" )?class=\"[^\"]*\\b{cssClass}\\b[^\"]*\"[^>]*>(.*?)</(?:td|th)>", RegexOptions.Singleline)
            .Select(m => Text(m.Groups[1].Value))];

    private static List<(string Value, string Text, bool Selected)> Options(string html, string selectId)
    {
        var select = Regex.Match(html, $"<select[^>]*id=\"{selectId}\"[^>]*>(.*?)</select>", RegexOptions.Singleline);
        Assert.True(select.Success, $"No <select id=\"{selectId}\"> was rendered.");

        return [.. Regex.Matches(select.Groups[1].Value, "<option([^>]*)>([^<]*)</option>")
            .Select(m => (Attr(m.Groups[1].Value, "value")!, Text(m.Groups[2].Value), Attr(m.Groups[1].Value, "selected") == "selected"))];
    }

    private static string Tick(int mode, int week, Guid id) => PackagingViewModel.TickKey(mode, week, id);

    private static void AssertButtons(string html, bool customs)
    {
        foreach (var (command, text) in new[] { ("select", "View"), ("save", "Save"), ("apply", "Apply"), ("labels", "Print labels") })
        {
            Assert.Matches($"<button[^>]*name=\"command\" value=\"{command}\"[^>]*>{text}</button>", html);
        }

        Assert.Matches("<button[^>]*id=\"packaging-print-button\"[^>]*>Print</button>", html);
        Assert.Matches("<button type=\"submit\" form=\"packaging-cancel-form\" class=\"govuk-button govuk-button--secondary\"[^>]*>Cancel</button>", html);
        Assert.Matches("<form id=\"packaging-cancel-form\" method=\"get\" class=\"noprint\" action=\"[^\"]*\"></form>|<form id=\"packaging-cancel-form\" method=\"get\" action=\"[^\"]*\" class=\"noprint\"></form>", html);
        Assert.DoesNotContain(">Cancel</a>", html, StringComparison.Ordinal);

        if (customs)
        {
            Assert.Matches("<button[^>]*name=\"command\" value=\"customs\"[^>]*>Print customs</button>", html);
        }
        else
        {
            Assert.DoesNotContain("Print customs", html, StringComparison.Ordinal);
            Assert.DoesNotContain("value=\"customs\"", html, StringComparison.Ordinal);
        }
    }

    // ---- 1 / 2: selection state ---------------------------------------------------------------

    [Fact]
    public async Task NoModeSelected_ShowsPleaseSelectTheThreeOptionsTheBlurbAndNoTable()
    {
        _apiClient.Packaging = Sheet(0, 1, Block("k1", Apr13, "Group A"));

        var html = await GetHtmlAsync(BaseQuery);

        Assert.Contains("<title>Sample packaging &#x2013; April 2026", html, StringComparison.Ordinal);
        Assert.Contains("Sample packaging – April 2026</h1>", html, StringComparison.Ordinal);

        Assert.Equal(
            [("", "Select", true), ("0", "Group address packaging", false), ("1", "Participant combined packaging", false), ("2", "Daily packaging", false)],
            Options(html, "packaging-mode"));

        Assert.Contains(BlurbText, html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<select class=\"govuk-select\" id=\"packaging-week\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-error-summary", html, StringComparison.Ordinal);
        AssertButtons(html, customs: false);
        Assert.Empty(_apiClient.PackagingRequests);
    }

    [Fact]
    public async Task GroupAddressWithNoWeek_ShowsTheWeekSelectWithPleaseSelectAndNoTable()
    {
        _apiClient.Packaging = Sheet(0, 1, Block("k1", Apr13, "Group A"));

        var html = await GetHtmlAsync($"{BaseQuery}&mode=0&week=0");

        var modes = Options(html, "packaging-mode");
        Assert.Equal(3, modes.Count);
        Assert.Equal([("0", "Group address packaging", true), ("1", "Participant combined packaging", false), ("2", "Daily packaging", false)], modes);

        Assert.Equal(
            [("0", "Select", true), ("1", "Week 1", false), ("2", "Week 2", false), ("3", "Week 3", false), ("4", "Week 4", false)],
            Options(html, "packaging-week"));

        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
        AssertButtons(html, customs: false);
        Assert.Empty(_apiClient.PackagingRequests);
    }

    [Fact]
    public async Task ParticipantCombinedWithNoWeek_ShowsNoTableAndNoPrintCustoms()
    {
        var html = await GetHtmlAsync($"{BaseQuery}&mode=1&week=0");

        Assert.Equal(["Participant combined packaging"], Options(html, "packaging-mode").Where(o => o.Selected).Select(o => o.Text));
        Assert.Equal(5, Options(html, "packaging-week").Count);
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
        AssertButtons(html, customs: false);
    }

    [Fact]
    public async Task GroupAddressWithAWeek_SelectsThatWeekWithoutPleaseSelect()
    {
        _apiClient.Packaging = Sheet(0, 3);

        var html = await GetHtmlAsync($"{BaseQuery}&mode=0&week=3");

        var weeks = Options(html, "packaging-week");
        Assert.Equal(["1", "2", "3", "4"], weeks.Select(w => w.Value));
        Assert.Equal(["Week 3"], weeks.Where(w => w.Selected).Select(w => w.Text));
        Assert.Equal((2026, 4, 0, 3), Assert.Single(_apiClient.PackagingRequests));
    }

    // ---- 3: Group Address ---------------------------------------------------------------------

    [Fact]
    public async Task GroupAddress_RendersLegacyColumnsRowsAndStageCells()
    {
        var complete = Scheme(1, "17152A/BA PT0060: Mastitis bovine - 9 Sets", packingInfo: "Scheme packing note",
            packedTotal: 1, postedTotal: 2, participants: [Part(11, "Lab 1: Alpha", packaged: Apr10, permit: "Licence A"), Part(12, "Lab 2: Beta", permit: "Licence B")]);
        var incomplete = Scheme(2, "17153A/BA PT0061: Brucella - 3 Sets", prep: false, participants: [Part(13, "Lab 3: Gamma")]);
        _apiClient.Packaging = Sheet(0, 1, Block("k1", Apr13, "Group A", "Group packing note", schemes: [complete, incomplete]));

        var html = await GetHtmlAsync($"{BaseQuery}&mode=0&week=1");

        Assert.Equal(
            ["Date", "Group address", "Print", "Scheme", "Packed", "Posted", "Import permit", "Packing instructions"],
            Headers(html));
        Assert.Equal((2026, 4, 0, 1), Assert.Single(_apiClient.PackagingRequests));

        // Group cells: long date then the group heading; the group packing note sits in its own column.
        Assert.Equal(["13 April 2026", "Group A"], Cells(html, "cellWorkSheetGroup"));
        Assert.Equal(["Group packing note"], Cells(html, "cellWorkSheetPacking"));
        Assert.Equal(["Scheme packing note"], Cells(html, "cellWorkSheetSchemePacking"));

        Assert.Equal(["Lab 1: Alpha", "Lab 2: Beta", "Lab 3: Gamma"], Cells(html, "cellWorkSheetParticipant"));
        Assert.Equal(["", "Licence A", "Licence B", "", "N/A"], Cells(html, "cellWorkSheetImportPermit"));

        // Scheme descriptions; only the incomplete one carries the tooltip and the hidden text.
        var schemes = Cells(html, "cellWorkSheetScheme");
        Assert.Equal("17152A/BA PT0060: Mastitis bovine - 9 Sets", schemes[0]);
        Assert.Contains("17153A/BA PT0061: Brucella - 3 Sets", schemes[1], StringComparison.Ordinal);
        Assert.Contains(PreparationIncomplete, schemes[1], StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(html, $"title=\"{PreparationIncomplete}\""));
        Assert.Equal(1, Regex.Count(html, "cellWorkSheetPreparationIncomplete"));

        // Per-participant cells: a recorded date replaces the checkbox, an incomplete scheme shows neither.
        var stage = Cells(html, "cellWorkSheetComplete");
        Assert.Equal(6, stage.Count);
        Assert.Equal("10 April 2026", stage[0]);
        Assert.Contains("Posted: Lab 1: Alpha, 17152A/BA PT0060: Mastitis bovine - 9 Sets", stage[1], StringComparison.Ordinal);
        Assert.Contains("Packed: Lab 2: Beta", stage[2], StringComparison.Ordinal);
        Assert.Equal(string.Empty, stage[4]);
        Assert.Equal(string.Empty, stage[5]);

        var packedTicks = Checkboxes(html, "PackedTicks");
        Assert.Equal([Tick(0, 1, Id(12))], packedTicks.Select(t => Attr(t, "value")!));
        Assert.All(packedTicks, t => Assert.Null(Attr(t, "checked")));
        Assert.Equal(
            [Tick(0, 1, Id(11)), Tick(0, 1, Id(12))],
            Checkboxes(html, "PostedTicks").Select(t => Attr(t, "value")!));

        // Scheme totals: "Complete" text, an "All" checkbox, or nothing.
        var totals = Cells(html, "cellWorkSheetCompleteTotal");
        Assert.Equal("Complete", totals[0]);
        Assert.Contains("All posted: 17152A/BA PT0060: Mastitis bovine - 9 Sets", totals[1], StringComparison.Ordinal);
        Assert.Equal(string.Empty, totals[2]);
        Assert.Equal(string.Empty, totals[3]);

        var allBoxes = Regex.Matches(html, "<input[^>]*ptl-tick-scheme[^>]*>").Select(m => m.Value).ToList();
        var allBox = Assert.Single(allBoxes);
        Assert.Equal("posted", Attr(allBox, "data-kind"));
        Assert.Equal(Id(1001).ToString(), Attr(allBox, "data-scheme"));
        Assert.Null(Attr(allBox, "name"));

        var print = Assert.Single(Checkboxes(html, "PrintKeys"));
        Assert.Equal("k1", Attr(print, "value"));
        Assert.Null(Attr(print, "checked"));
        Assert.Matches("Print Group A</span>", html);

        Assert.DoesNotContain("All packed:", html, StringComparison.Ordinal);
        AssertButtons(html, customs: false);
        Assert.Contains("<title>Sample packaging &#x2013; April 2026", html, StringComparison.Ordinal);
        Assert.Contains("Sample Packaging - Week 1 - April 2026 - Group Packaging", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroupAddress_PackedCompletePostedSelectable_AndShowAllFlagsAreIgnoredOutsideParticipantCombined()
    {
        var scheme = Scheme(1, "Scheme A", packedTotal: 2, postedTotal: 1, participants: [Part(11, "Lab 1", packaged: Apr10, posted: Apr11)]);
        _apiClient.Packaging = Sheet(0, 2, Block("k1", Apr13, "Group A", showAllPacked: true, showAllPosted: true, schemes: [scheme]));

        var html = await GetHtmlAsync($"{BaseQuery}&mode=0&week=2");

        var totals = Cells(html, "cellWorkSheetCompleteTotal");
        Assert.Contains("All packed: Scheme A", totals[0], StringComparison.Ordinal);
        Assert.Equal("Complete", totals[1]);

        // The block-level "All packed:/All posted:" controls belong to Participant Combined only.
        Assert.DoesNotContain("<input class=\"govuk-checkboxes__input ptl-tick-block", html, StringComparison.Ordinal);
        Assert.Equal(["10 April 2026", "11 April 2026"], Cells(html, "cellWorkSheetComplete"));
    }

    // ---- 4: Participant Combined --------------------------------------------------------------

    [Fact]
    public async Task ParticipantCombined_RendersSchemeLevelStageCellsPackingLinesAndAllControls()
    {
        var s1 = Scheme(1, "S1 desc", packingInfo: "Scheme packing", customerPackingInfo: "Customer packing", psLevelPackingInfo: "PS level packing",
            permit: "Permit OK", ownRow: 21, posted: Apr11);
        var s2 = Scheme(2, "S2 desc", permit: "N/A", ownRow: 22, packaged: Apr12);
        var s3 = Scheme(3, "S3 desc", prep: false, ownRow: 23);
        var s4 = Scheme(4, "S4 desc");
        var b1 = Block("k2", Apr13, "LAB01: Lab One", "Participant packing", showAllPacked: true, showAllPosted: true, schemes: [s1, s2, s3, s4]);
        var b2 = Block("k3", Apr13, "LAB02: Lab Two", showAllPacked: true, schemes: [Scheme(5, "S5 desc", ownRow: 25)]);
        var b3 = Block("k4", Apr13, "LAB03: Lab Three", schemes: [Scheme(6, "S6 desc", ownRow: 26)]);
        _apiClient.Packaging = Sheet(1, 2, b1, b2, b3);

        var html = await GetHtmlAsync($"{BaseQuery}&mode=1&week=2");

        Assert.Equal(
            ["Date", "Participant", "Print", "Scheme", "Packed", "Posted", "Import permit", "Packing instructions"],
            Headers(html));
        Assert.Equal((2026, 4, 1, 2), Assert.Single(_apiClient.PackagingRequests));

        var groups = Cells(html, "cellWorkSheetGroup");
        Assert.Equal(6, groups.Count);
        Assert.Contains("LAB01: Lab One", groups[1], StringComparison.Ordinal);
        Assert.Contains("All packed:", groups[1], StringComparison.Ordinal);
        Assert.Contains("All posted:", groups[1], StringComparison.Ordinal);
        Assert.Contains("All packed:", groups[3], StringComparison.Ordinal);
        Assert.DoesNotContain("All posted:", groups[3], StringComparison.Ordinal);
        Assert.Equal("LAB03: Lab Three", groups[5]);

        var blockTicks = Regex.Matches(html, "<input[^>]*ptl-tick-block[^>]*>").Select(m => Attr(m.Value, "data-kind")).ToList();
        Assert.Equal(["packed", "posted", "packed"], blockTicks);

        Assert.Equal(["Participant packing", "", ""], Cells(html, "cellWorkSheetPacking"));
        Assert.Equal(["Scheme packing", "Customer packing", "PS level packing"], Cells(html, "cellWorkSheetSchemePacking"));

        // Own-row ticks: only rows that are prepared, have an id and no date yet.
        Assert.Equal(
            [Tick(1, 2, Id(21)), Tick(1, 2, Id(25)), Tick(1, 2, Id(26))],
            Checkboxes(html, "PackedTicks").Select(t => Attr(t, "value")!));
        Assert.Equal(
            [Tick(1, 2, Id(22)), Tick(1, 2, Id(25)), Tick(1, 2, Id(26))],
            Checkboxes(html, "PostedTicks").Select(t => Attr(t, "value")!));

        var totals = Cells(html, "cellWorkSheetCompleteTotal");
        Assert.Contains("Packed: LAB01: Lab One, S1 desc", totals[0], StringComparison.Ordinal);
        Assert.Equal("11 April 2026", totals[1]);
        Assert.Equal("12 April 2026", totals[2]);
        Assert.Contains("Posted: LAB01: Lab One, S2 desc", totals[3], StringComparison.Ordinal);

        Assert.Equal("Permit OK", Cells(html, "cellWorkSheetImportPermit")[0]);
        Assert.Equal("N/A", Cells(html, "cellWorkSheetImportPermit")[1]);

        Assert.Equal(1, Regex.Count(html, $"title=\"{PreparationIncomplete}\""));
        AssertButtons(html, customs: true);
        Assert.Contains("Sample Packaging - Week 2 - April 2026 - Participant Combined Packaging", html, StringComparison.Ordinal);
    }

    // ---- 5: Daily -----------------------------------------------------------------------------

    [Fact]
    public async Task Daily_RendersDayColumnsDueToBePostedRowsAndPrintCustoms()
    {
        var due = Scheme(1, "Daily Scheme desc", packedTotal: 2, packingInfo: "Daily scheme packing",
            participants: [Part(31, "Lab X", permit: "Permit X"), Part(32, "Lab Y", packaged: Apr10, posted: Apr11)]);
        var notDue = Scheme(2, "Other scheme desc", postedTotal: 2, participants: [Part(33, "Lab Z")]);
        _apiClient.Packaging = Sheet(2, 0,
            Block("d1", Apr13, "PT0060", due: true, schemes: [due]),
            Block("d2", Apr12, "PT0061", schemes: [notDue]),
            Block("d3", Apr11, "PT0062"));

        var html = await GetHtmlAsync($"{BaseQuery}&mode=2&week=0");

        Assert.Equal(["Day", "Print", "Scheme", "Packed", "Posted", "Import permit"], Headers(html));
        Assert.Empty(Cells(html, "cellWorkSheetPacking"));
        Assert.Empty(Cells(html, "cellWorkSheetGroup"));
        Assert.DoesNotContain("<select class=\"govuk-select\" id=\"packaging-week\"", html, StringComparison.Ordinal);
        Assert.Equal((2026, 4, 2, 0), Assert.Single(_apiClient.PackagingRequests));

        // Only the due block's rows (scheme row + its two participant rows) are highlighted.
        Assert.Equal(3, Regex.Count(html, "<tr class=\"govuk-table__row rowDueToBePosted\""));
        Assert.Equal(1, Regex.Count(html, "Due to be posted</strong>"));
        Assert.Contains("13 April 2026", html, StringComparison.Ordinal);
        Assert.Contains("12 April 2026", html, StringComparison.Ordinal);
        Assert.Contains("11 April 2026", html, StringComparison.Ordinal);

        Assert.Equal(["Daily scheme packing"], Cells(html, "cellWorkSheetSchemePacking"));
        Assert.Equal(["Lab X", "Lab Y", "Lab Z"], Cells(html, "cellWorkSheetParticipant"));
        Assert.Equal("Permit X", Cells(html, "cellWorkSheetImportPermit")[1]);

        // Daily print labels are "{block heading}: {first scheme}", or the heading alone with no schemes.
        Assert.Contains("Print PT0060: Daily Scheme desc", html, StringComparison.Ordinal);
        Assert.Contains("Print PT0061: Other scheme desc", html, StringComparison.Ordinal);
        Assert.Matches(">Print PT0062</span>", html);
        Assert.Equal(["d1", "d2", "d3"], Checkboxes(html, "PrintKeys").Select(t => Attr(t, "value")!));

        Assert.Contains("All packed: Daily Scheme desc", html, StringComparison.Ordinal);
        Assert.Contains("All posted: Other scheme desc", html, StringComparison.Ordinal);
        AssertButtons(html, customs: true);
        Assert.Contains("Sample Packaging - April 2026 - Weekly", html, StringComparison.Ordinal);
    }

    // ---- 6: empty / zero-scheme blocks --------------------------------------------------------

    [Theory]
    [InlineData(0, 1, 8)]
    [InlineData(1, 1, 8)]
    [InlineData(2, 0, 6)]
    public async Task EmptyBlocks_ShowTheNoSamplesRowSpanningEveryColumn(int mode, int week, int columns)
    {
        _apiClient.Packaging = Sheet(mode, week);

        var html = await GetHtmlAsync($"{BaseQuery}&mode={mode}&week={week}");

        Assert.Equal(["There are no samples to be packed"], Cells(html, "cellWorkSheetNoSchemes"));
        Assert.Contains($"colspan=\"{columns}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-block=", html, StringComparison.Ordinal);
        Assert.Equal(columns, Headers(html).Count);
    }

    [Fact]
    public async Task BlockWithNoSchemes_StillRendersItsDateHeadingAndPrintCheckbox()
    {
        _apiClient.Packaging = Sheet(0, 1, Block("k9", Apr13, "Group Empty", "Ignored packing note"));

        var html = await GetHtmlAsync($"{BaseQuery}&mode=0&week=1");

        Assert.Equal(["13 April 2026", "Group Empty"], Cells(html, "cellWorkSheetGroup"));
        Assert.Equal("k9", Attr(Assert.Single(Checkboxes(html, "PrintKeys")), "value"));
        Assert.Contains("data-block=\"0\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("There are no samples to be packed", html, StringComparison.Ordinal);
        Assert.Empty(Cells(html, "cellWorkSheetScheme"));
    }

    // ---- 7: carried ticks ---------------------------------------------------------------------

    [Fact]
    public async Task Select_CarriesTicksForRowsNotOnScreenAsHiddenInputs_AndChecksRowsThatAre()
    {
        var scheme = Scheme(1, "Scheme A", participants: [Part(41, "Lab 1"), Part(42, "Lab 2")]);
        _apiClient.Packaging = Sheet(0, 1, Block("k1", Apr13, "Group A", schemes: [scheme]));

        var html = await PostHtmlAsync(
            ("command", "select"),
            ("Mode", "0"),
            ("Week", "1"),
            ("PackedTicks", Tick(0, 1, Id(41))),
            ("PackedTicks", Tick(1, 2, Id(99))),
            ("PackedTicks", Tick(1, 2, Id(99))),
            ("PostedTicks", Tick(0, 1, Id(42))),
            ("PostedTicks", Tick(2, 0, Id(98))),
            ("PrintKeys", "k1"));

        Assert.Equal([Tick(1, 2, Id(99))], HiddenInputs(html, "PackedTicks").Select(t => Attr(t, "value")!));
        Assert.Equal([Tick(2, 0, Id(98))], HiddenInputs(html, "PostedTicks").Select(t => Attr(t, "value")!));

        var packed = Checkboxes(html, "PackedTicks");
        Assert.Equal([Tick(0, 1, Id(41)), Tick(0, 1, Id(42))], packed.Select(t => Attr(t, "value")!));
        Assert.Equal(["checked", null], packed.Select(t => Attr(t, "checked")));

        var posted = Checkboxes(html, "PostedTicks");
        Assert.Equal([null, "checked"], posted.Select(t => Attr(t, "checked")));

        Assert.Equal("checked", Attr(Assert.Single(Checkboxes(html, "PrintKeys")), "checked"));
        Assert.Equal((2026, 4, 0, 1), Assert.Single(_apiClient.PackagingRequests));
    }

    [Fact]
    public async Task Select_WithNoModeButCarriedTicks_KeepsEveryTickHidden()
    {
        var html = await PostHtmlAsync(
            ("command", "select"),
            ("PackedTicks", Tick(0, 1, Id(41))),
            ("PostedTicks", Tick(0, 1, Id(42))));

        Assert.Equal([Tick(0, 1, Id(41))], HiddenInputs(html, "PackedTicks").Select(t => Attr(t, "value")!));
        Assert.Equal([Tick(0, 1, Id(42))], HiddenInputs(html, "PostedTicks").Select(t => Attr(t, "value")!));
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
    }

    // ---- 8: errors ----------------------------------------------------------------------------

    [Fact]
    public async Task Customs_WhenTheMailMergeFails_ShowsTheMessageAndDetailAsAnError()
    {
        _apiClient.Packaging = Sheet(1, 2, Block("k2", Apr13, "LAB01: Lab One", schemes: [Scheme(1, "S1 desc", ownRow: 21)]));
        _apiClient.PackagingCustoms = new PackagingCustomsResult(null, string.Empty, "The Customs template could not be found");

        var html = await PostHtmlAsync(("command", "customs"), ("Mode", "1"), ("Week", "2"), ("PrintKeys", "k2"));

        Assert.Contains("<title>Error: Sample packaging &#x2013; April 2026", html, StringComparison.Ordinal);
        Assert.Contains("data-module=\"govuk-error-summary\"", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"govuk-error-summary__title\">There is a problem</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<li>There was a problem with the Mail Merge</li>", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"govuk-body\">The Customs template could not be found</p>", html, StringComparison.Ordinal);

        // The sheet is still shown beneath the error, and the Customs request carried the ticked rows.
        Assert.Contains("id=\"packaging-table\"", html, StringComparison.Ordinal);
        var request = Assert.Single(_apiClient.PackagingCustomsRequests);
        Assert.Equal((1, 2), (request.Mode, request.Week));
        Assert.Equal(["k2"], request.SelectedKeys);
    }

    [Fact]
    public async Task Customs_WhenTheMailMergeFailsWithoutADetail_ShowsOnlyTheMessage()
    {
        _apiClient.Packaging = Sheet(2, 0);
        _apiClient.PackagingCustoms = new PackagingCustomsResult(null, string.Empty, null);

        var html = await PostHtmlAsync(("command", "customs"), ("Mode", "2"), ("Week", "0"));

        Assert.Contains("<li>There was a problem with the Mail Merge</li>", html, StringComparison.Ordinal);
        var summaryStart = html.IndexOf("govuk-error-summary__body", StringComparison.Ordinal);
        Assert.DoesNotContain("<p class=\"govuk-body\">", html[summaryStart..html.IndexOf("</ul>", summaryStart, StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.Contains("<title>Error: ", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Customs_WhenTheDocumentIsBuilt_ReturnsTheWordFileInsteadOfAPage()
    {
        _apiClient.Packaging = Sheet(2, 0);
        _apiClient.PackagingCustoms = new PackagingCustomsResult([1, 2, 3], "Customs.docx", null);

        var response = await PostAsync(_factory.CreateClient(), ("command", "customs"), ("Mode", "2"), ("Week", "0"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Labels_WhenThereAreNone_RedisplaysThePageWithNoLabelsToPrintAsAnError()
    {
        _apiClient.Packaging = Sheet(0, 1, Block("k1", Apr13, "Group A", schemes: [Scheme(1, "Scheme A")]));
        _apiClient.PackagingLabels = new PackagingLabelsResponse([]);

        var html = await PostHtmlAsync(("command", "labels"), ("Mode", "0"), ("Week", "1"), ("PrintKeys", "k1"));

        Assert.Contains("<title>Error: Sample packaging &#x2013; April 2026", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"govuk-error-summary__title\">There is a problem</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<li>No Labels to Print</li>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"packaging-table\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ptl-label", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Labels_WithNoOptionChosen_ShowsValidationErrorWithoutATable()
    {
        var html = await PostHtmlAsync(("command", "labels"));

        Assert.Contains("<title>Error: Sample packaging &#x2013;", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"#packaging-mode\">Select a packaging option</a>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"packaging-mode-error\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
        Assert.Empty(_apiClient.PackagingLabelRequests);
    }

    // ---- 9: notification banner ---------------------------------------------------------------

    [Fact]
    public async Task Apply_Succeeds_RedirectsBackToThePageWithASuccessBanner()
    {
        _apiClient.Packaging = Sheet(0, 1, Block("k1", Apr13, "Group A", schemes: [Scheme(1, "Scheme A", participants: [Part(41, "Lab 1")])]));
        _apiClient.PackagingSaveResult = new PackagingSaveResult(true);

        var client = _factory.CreateClient();
        var response = await PostAsync(client, ("command", "apply"), ("Mode", "0"), ("Week", "1"), ("PostedTicks", Tick(0, 1, Id(41))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("govuk-notification-banner govuk-notification-banner--success", html, StringComparison.Ordinal);
        Assert.Contains("Sample Packaging updated successfully.", html, StringComparison.Ordinal);
        Assert.Contains("id=\"packaging-table\"", html, StringComparison.Ordinal);

        var save = Assert.Single(_apiClient.SavedPackagingRequests);
        var update = Assert.Single(save.Updates);
        Assert.Equal(new PackagingStageUpdateRequest(0, 1, Id(41), Packed: false, Posted: true), update);
    }

    [Fact]
    public async Task Save_Fails_RedirectsBackToThePageWithAnErrorBanner()
    {
        _apiClient.Packaging = Sheet(0, 1);
        _apiClient.PackagingSaveResult = new PackagingSaveResult(false);

        var response = await PostAsync(_factory.CreateClient(), ("command", "save"), ("Mode", "0"), ("Week", "1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("govuk-notification-banner", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-notification-banner--success", html, StringComparison.Ordinal);
        Assert.Contains("Sample Packaging could not be saved. Please try again later.", html, StringComparison.Ordinal);
        Assert.Contains("id=\"packaging-table\"", html, StringComparison.Ordinal);
    }

    // ---- 10: labels view ----------------------------------------------------------------------

    [Fact]
    public async Task Labels_WhenReturned_RendersOneBlockPerLabelWithLinesInOrderAndABackLink()
    {
        _apiClient.Packaging = Sheet(1, 2);
        _apiClient.PackagingLabels = new PackagingLabelsResponse(
        [
            new PackagingLabelResponse(["LAB01", "Lab One & Co", "1 High Street"]),
            new PackagingLabelResponse(["LAB02", "Lab Two"]),
        ]);

        var html = await PostHtmlAsync(("command", "labels"), ("Mode", "1"), ("Week", "2"), ("PrintKeys", "k2"));

        Assert.Contains("<title>Sample Packaging Labels - April 2026", html, StringComparison.Ordinal);
        Assert.Contains("Sample Packaging Labels - April 2026</h1>", html, StringComparison.Ordinal);
        Assert.Matches("<button[^>]*id=\"labels-print-button\"[^>]*>Print</button>", html);

        var blocks = Regex.Matches(html, "<div class=\"ptl-label\">(.*?)</div>", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(2, blocks.Count);
        Assert.Equal(
            ["LAB01", "Lab One & Co", "1 High Street"],
            Regex.Matches(blocks[0], "<p class=\"govuk-body ptl-label__line\">(.*?)</p>").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)));
        Assert.Equal(
            ["LAB02", "Lab Two"],
            Regex.Matches(blocks[1], "<p class=\"govuk-body ptl-label__line\">(.*?)</p>").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)));
        Assert.Contains("Lab One &amp; Co", html, StringComparison.Ordinal);

        var back = Regex.Match(html, "<a class=\"govuk-back-link noprint\" href=\"([^\"]*)\">Back</a>");
        Assert.True(back.Success);
        var href = WebUtility.HtmlDecode(back.Groups[1].Value);
        Assert.StartsWith("/Distribution/Packaging?", href, StringComparison.Ordinal);
        var query = System.Web.HttpUtility.ParseQueryString(href[(href.IndexOf('?') + 1)..]);
        Assert.Equal("2026", query["year"]);
        Assert.Equal("4", query["monthId"]);
        Assert.Equal("1", query["mode"]);
        Assert.Equal("2", query["week"]);

        var request = Assert.Single(_apiClient.PackagingLabelRequests);
        Assert.Equal((1, 2), (request.Mode, request.Week));
        Assert.Equal(["k2"], request.SelectedKeys);
    }
}
