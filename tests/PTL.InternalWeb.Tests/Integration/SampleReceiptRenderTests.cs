using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/SampleReceipt.cshtml - legacy Internal User/SampleReceipt.aspx.
public partial class SampleReceiptRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private static readonly Guid Scheme = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid Participant = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid TestId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid Sample1 = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid Sample2 = Guid.Parse("55555555-0000-0000-0000-000000000002");
    private static readonly Guid Sample3 = Guid.Parse("55555555-0000-0000-0000-000000000003");

    private static readonly string Url = $"/Distribution/SampleReceipt?year=2026&monthId=5&schemeId={Scheme}&participantId={Participant}";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public SampleReceiptRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private static SampleReceiptResponse Page(bool readOnly = false) =>
        new("D1a/S1 : BT Brucella", "Labcode L001: Lab One", readOnly,
        [
            new SampleReceiptTestResponse(TestId, "Culture",
            [
                new SampleReceiptSampleResponse(Sample1, "S001", true, false, false),
                new SampleReceiptSampleResponse(Sample2, "S002", false, true, false),
                new SampleReceiptSampleResponse(Sample3, "S003", false, false, false),
            ]),
        ]);

    private async Task<string> GetHtmlAsync()
    {
        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Text(string fragment) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim());

    private static List<string> Rows(string html) =>
        [.. Regex.Matches(html, "<tr .*?</tr>", RegexOptions.Singleline).Select(m => m.Value)];

    private static string Checkbox(string html, string name) =>
        Regex.Matches(html, "<input[^>]*>").Select(m => m.Value)
            .Single(t => t.Contains($"name=\"{name}\"", StringComparison.Ordinal) && t.Contains("type=\"checkbox\"", StringComparison.Ordinal));

    [Fact]
    public async Task Get_RendersLegacyTitleHeadingsTestTypeAndTickBoxColumns()
    {
        _apiClient.SampleReceipt = Page();

        var html = await GetHtmlAsync();

        Assert.Equal("Submission Status", Text(Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value).Split(" - ")[0]);
        Assert.Equal("Submission Status", Text(Regex.Match(html, "<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value));
        var headings = Regex.Matches(html, "<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline).Select(m => Text(m.Groups[1].Value)).ToList();
        Assert.Equal(["D1a/S1 : BT Brucella", "Labcode L001: Lab One", "Culture"], headings.Take(3));
        Assert.Contains("Submission Status for Culture", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sample Receipt", html, StringComparison.Ordinal);

        var thead = Regex.Match(html, "<thead.*?</thead>", RegexOptions.Singleline).Value;
        Assert.Equal(
            ["Sample", "Submitted", "Not submitted", "Not in reportoire"],
            Regex.Matches(thead, "<th [^>]*>(.*?)</th>", RegexOptions.Singleline).Select(m => Text(m.Groups[1].Value)));
    }

    [Fact]
    public async Task Get_RendersASampleRowWithThreeTickBoxesReflectingTheStoredStatus()
    {
        _apiClient.SampleReceipt = Page();

        var html = await GetHtmlAsync();

        var rows = Rows(html).Where(r => r.Contains("Sample: S", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, rows.Count);
        Assert.Contains("checked=\"checked\"", Checkbox(rows[0], "Tests[0].Samples[0].Submitted"), StringComparison.Ordinal);
        Assert.DoesNotContain("checked=", Checkbox(rows[0], "Tests[0].Samples[0].NotSubmitted"), StringComparison.Ordinal);
        Assert.Contains("checked=\"checked\"", Checkbox(rows[1], "Tests[0].Samples[1].NotSubmitted"), StringComparison.Ordinal);
        Assert.DoesNotContain("checked=", Checkbox(rows[2], "Tests[0].Samples[2].NotInRepertoire"), StringComparison.Ordinal);
        Assert.Contains($"value=\"{Sample1}\"", rows[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_EveryTickBoxHasAnAccessibleNameThatIdentifiesTheSample()
    {
        _apiClient.SampleReceipt = Page();

        var html = await GetHtmlAsync();

        Assert.Contains("Submitted for sample S001", html, StringComparison.Ordinal);
        Assert.Contains("Not submitted for sample S002", html, StringComparison.Ordinal);
        Assert.Contains("Not in reportoire for sample S003", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_Editable_RendersSaveAndCancelAndTheFormPostsToTheSameRoute()
    {
        _apiClient.SampleReceipt = Page();

        var html = await GetHtmlAsync();

        var buttons = Regex.Matches(html, "<button type=\"submit\" name=\"command\" value=\"(\\w+)\"[^>]*>(.*?)</button>", RegexOptions.Singleline)
            .Select(m => (Value: m.Groups[1].Value, Label: Text(m.Groups[2].Value)))
            .ToList();
        Assert.Equal([("save", "Save"), ("cancel", "Cancel")], buttons);
        Assert.Matches("<form[^>]*action=\"/Distribution/SampleReceipt\\?year=2026&amp;monthId=5&amp;schemeId=[^\"]+&amp;participantId=[^\"]+\"", html);
        Assert.DoesNotContain("govuk-back-link", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_ReadOnly_ShowsTheStatusAsTextWithAnExplanationAndOnlyABackButton()
    {
        _apiClient.SampleReceipt = Page(readOnly: true);

        var html = await GetHtmlAsync();

        Assert.Contains("The samples can no longer be changed because the results are ready for the assessor.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<input", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"command\"", html, StringComparison.Ordinal);

        var rows = Rows(html).Where(r => r.Contains("Sample: S", StringComparison.Ordinal)).Select(Text).ToList();
        Assert.Equal(["Sample: S001 Submitted", "Sample: S002 Not submitted", "Sample: S003 No status recorded"], rows);
        Assert.Matches("<a class=\"govuk-button govuk-button--secondary\"[^>]*href=\"/Distribution/ResultsReceipt\\?year=2026&amp;monthId=5\"[^>]*>Back</a>", html);
    }

    [Fact]
    public async Task Get_NoTests_ShowsTheLegacyMessageAndOnlyABackButton()
    {
        _apiClient.SampleReceipt = new SampleReceiptResponse("D1a/S1 : BT Brucella", "Labcode L001: Lab One", true, []);

        var html = await GetHtmlAsync();

        Assert.Contains("There are no samples allocated to tests for this distribution.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
        Assert.Matches("<a class=\"govuk-button govuk-button--secondary\"[^>]*>Back</a>", html);
    }

    [Fact]
    public async Task Get_ApiReturnsNull_Returns404()
    {
        _apiClient.SampleReceipt = null;

        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Save_SendsTheTicksToTheApiAndLeavesForResultsReceiptWithASuccessBanner()
    {
        _apiClient.SampleReceipt = Page();
        _apiClient.ResultsReceipt = new ResultsReceiptResponse(Guid.NewGuid(), 2026, 5, []);
        _apiClient.SampleReceiptSaveResult = new SampleReceiptSaveResult(true);
        var client = _factory.CreateClient();
        var token = AntiforgeryTokenPattern().Match(await (await client.GetAsync(Url)).Content.ReadAsStringAsync()).Groups[1].Value;

        var response = await client.PostAsync(Url, new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("command", "save"),
            new("Tests[0].Samples[0].SampleReceiptId", Sample1.ToString()),
            new("Tests[0].Samples[0].Submitted", "false"),
            new("Tests[0].Samples[0].NotSubmitted", "true"),
            new("Tests[0].Samples[0].NotSubmitted", "false"),
            new("Tests[0].Samples[0].NotInRepertoire", "false"),
        ]));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Submission Status updated successfully.", html, StringComparison.Ordinal);
        Assert.Contains("Results Receipt - May 2026", html, StringComparison.Ordinal);

        var sample = Assert.Single(Assert.Single(_apiClient.SavedSampleReceiptRequests).Samples);
        Assert.Equal(new SampleReceiptSampleUpdateRequest(Sample1, false, true, false), sample);
    }
}
