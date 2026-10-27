using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/ActualResults.cshtml through the real view engine - legacy
// Internal User/ActualResults.aspx parity.
public partial class ActualResultsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private static readonly Guid Scheme = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid Participant = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid TestId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid EmptyTestId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000002");
    private static readonly Guid DropdownItem = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TextItem = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid Sample1 = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid Sample2 = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid Value11 = Guid.Parse("33333333-0000-0000-0000-000000000011");
    private static readonly Guid Value12 = Guid.Parse("33333333-0000-0000-0000-000000000012");
    private static readonly Guid Value21 = Guid.Parse("33333333-0000-0000-0000-000000000021");
    private static readonly Guid Value22 = Guid.Parse("33333333-0000-0000-0000-000000000022");
    private static readonly Guid MethodText = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid MethodDropdown = Guid.Parse("44444444-0000-0000-0000-000000000002");

    private static readonly string Url =
        $"/Distribution/ActualResults?year=2026&monthId=5&schemeId={Scheme}&participantId={Participant}";

    private const string Heading = "D1a S1: BT Brucella";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public ActualResultsRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private static ActualResultsResponse Page() =>
        new(Participant, Scheme, Heading, "Results for L001: Lab One", new DateTime(2026, 6, 30), IsNew: false,
        [
            new ActualResultsTestResponse(TestId, "Culture test",
                [
                    new ActualResultsResultItemResponse(DropdownItem, "Growth", "Positive|Negative", "Positive or Negative"),
                    new ActualResultsResultItemResponse(TextItem, "Count", "^\\d+$", "Whole number"),
                ],
                [
                    new ActualResultsMethodItemResponse(MethodText, "Incubation", "^.+$", "Text", "37C"),
                    new ActualResultsMethodItemResponse(MethodDropdown, "Medium", "Agar|Broth", "Agar or Broth", "Broth"),
                ],
                [
                    new ActualResultsSampleRowResponse(Sample1, "S001",
                    [
                        new ActualResultsSampleValueResponse(Value11, DropdownItem, "Positive"),
                        new ActualResultsSampleValueResponse(Value12, TextItem, "12"),
                    ]),
                    new ActualResultsSampleRowResponse(Sample2, "S002",
                    [
                        new ActualResultsSampleValueResponse(Value21, DropdownItem, ""),
                        new ActualResultsSampleValueResponse(Value22, TextItem, ""),
                    ]),
                ]),
            new ActualResultsTestResponse(EmptyTestId, "Empty test", [], [], []),
        ]);

    private async Task<string> GetHtmlAsync()
    {
        var response = await _factory.CreateClient().GetAsync(Url);

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

    private static string? InputTag(string html, string name) =>
        Regex.Matches(html, "<input[^>]*>").Select(m => m.Value)
            .FirstOrDefault(t => t.Contains($"name=\"{name}\"", StringComparison.Ordinal) && !t.Contains("type=\"hidden\"", StringComparison.Ordinal));

    private static string? SelectTag(string html, string name) =>
        Regex.Matches(html, "<select[^>]*>.*?</select>", RegexOptions.Singleline).Select(m => m.Value)
            .FirstOrDefault(t => t.Contains($"name=\"{name}\"", StringComparison.Ordinal));

    private static string Title(string html) => Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value;

    private static string H1(string html) => Text(Regex.Match(html, "<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value);

    private static string H2(string html) => Text(Regex.Match(html, "<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline).Groups[1].Value);

    // The whole first-step form, as the browser would post it.
    private static (string Name, string Value)[] FormFields(string sample1Count = "12") =>
    [
        ("YearId", "2026"), ("MonthId", "5"), ("MonthlyDistributionSchemeId", Scheme.ToString()),
        ("DistributionParticipantId", Participant.ToString()), ("IsAuditStep", "False"),
        ("Tests[0].Samples[0].Values[0].SampleResultItemId", Value11.ToString()),
        ("Tests[0].Samples[0].Values[0].TestResultItemId", DropdownItem.ToString()),
        ("Tests[0].Samples[0].Values[0].Result", "Positive"),
        ("Tests[0].Samples[0].Values[1].SampleResultItemId", Value12.ToString()),
        ("Tests[0].Samples[0].Values[1].TestResultItemId", TextItem.ToString()),
        ("Tests[0].Samples[0].Values[1].Result", sample1Count),
        ("Tests[0].Samples[1].Values[0].SampleResultItemId", Value21.ToString()),
        ("Tests[0].Samples[1].Values[0].TestResultItemId", DropdownItem.ToString()),
        ("Tests[0].Samples[1].Values[0].Result", "Negative"),
        ("Tests[0].Samples[1].Values[1].SampleResultItemId", Value22.ToString()),
        ("Tests[0].Samples[1].Values[1].TestResultItemId", TextItem.ToString()),
        ("Tests[0].Samples[1].Values[1].Result", "7"),
        ("Tests[0].MethodItems[0].TestMethodItemId", MethodText.ToString()),
        ("Tests[0].MethodItems[0].Result", "37C"),
        ("Tests[0].MethodItems[1].TestMethodItemId", MethodDropdown.ToString()),
        ("Tests[0].MethodItems[1].Result", "Broth"),
    ];

    private async Task<HttpResponseMessage> PostAsync(string command, params (string Name, string Value)[] fields)
    {
        var client = _factory.CreateClient();
        var form = await client.GetAsync(Url);
        var token = AntiforgeryTokenPattern().Match(await form.Content.ReadAsStringAsync()).Groups[1].Value;

        var content = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token), new("command", command) };
        content.AddRange((fields.Length == 0 ? FormFields() : fields).Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));

        return await client.PostAsync(Url, new FormUrlEncodedContent(content));
    }

    [Fact]
    public async Task Get_RendersLegacyTitleHeadingIntroAndButtons()
    {
        _apiClient.ActualResults = Page();

        var html = await GetHtmlAsync();

        Assert.Contains($"Results - {Heading}", Title(html), StringComparison.Ordinal);
        Assert.Equal("Results", H1(html));
        Assert.Equal(Heading, H2(html));
        var decoded = WebUtility.HtmlDecode(html);
        var first = decoded.IndexOf(">Enter the Test Results below.<", StringComparison.Ordinal);
        var second = decoded.IndexOf(">Tests are divided into Result Items which are tested for each Sample, and Method Items which apply to the Test itself.<", StringComparison.Ordinal);
        var third = decoded.IndexOf(">Results can be saved at any time up until the deadline date provided that all the inputs are valid.<", StringComparison.Ordinal);
        Assert.True(first >= 0 && second > first && third > second);

        var buttons = Regex.Matches(html, "<button type=\"submit\" name=\"command\" value=\"(\\w+)\"[^>]*>(.*?)</button>", RegexOptions.Singleline)
            .Select(m => (Value: m.Groups[1].Value, Label: Text(m.Groups[2].Value)))
            .ToList();
        Assert.Equal([("save", "Save"), ("cancel", "Cancel")], buttons);
        Assert.Matches("<button[^>]*value=\"cancel\"[^>]*formnovalidate", html);
        Assert.DoesNotContain("govuk-back-link", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_PastDeadline_RendersReadOnlyWithExplanationValuesAsTextAndOnlyABackButton()
    {
        _apiClient.ActualResults = Page() with { IsReadOnly = true };

        var html = await GetHtmlAsync();

        Assert.Equal("Results", H1(html));
        Assert.Equal(Heading, H2(html));
        Assert.Contains("The deadline date for these results has passed.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<input", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<select", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"command\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Enter the Test Results below.", html, StringComparison.Ordinal);

        var table = Text(Regex.Match(html, "<table.*?</table>", RegexOptions.Singleline).Value);
        Assert.Contains("Positive", table, StringComparison.Ordinal);
        Assert.Contains("12", table, StringComparison.Ordinal);
        Assert.Contains("37C", table, StringComparison.Ordinal);
        Assert.Contains("No result entered", table, StringComparison.Ordinal);

        Assert.Matches("<a class=\"govuk-button govuk-button--secondary\"[^>]*href=\"/Distribution/ResultsReceipt\\?year=2026&amp;monthId=5\"[^>]*>Back</a>", html);
    }

    [Fact]
    public async Task Get_RendersPerTestTableWithSampleRowsResultItemColumnsAndMethodItemRows()
    {
        _apiClient.ActualResults = Page();

        var html = await GetHtmlAsync();

        Assert.Contains(">Culture test</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<caption class=\"govuk-table__caption govuk-visually-hidden\">Actual Results for Culture test</caption>", html, StringComparison.Ordinal);

        var thead = Regex.Match(html, "<thead.*?</thead>", RegexOptions.Singleline).Value;
        var headers = Regex.Matches(thead, "<th [^>]*>(.*?)</th>", RegexOptions.Singleline).Select(m => Text(m.Groups[1].Value)).ToList();
        Assert.Equal(["Sample", "Growth", "Count Input must be of format: Whole number"], headers);

        var rowHeaders = Regex.Matches(html, "<th scope=\"row\"[^>]*>(.*?)</th>", RegexOptions.Singleline).Select(m => Text(m.Groups[1].Value)).ToList();
        Assert.Equal(["S001", "S002", "Incubation:", "Medium:"], rowHeaders);

        var methodRow = Regex.Matches(html, "<tr .*?</tr>", RegexOptions.Singleline).Select(m => m.Value).Single(r => r.Contains("Incubation:", StringComparison.Ordinal));
        Assert.Contains("colspan=\"2\"", methodRow, StringComparison.Ordinal);
        Assert.Contains("value=\"37C\"", InputTag(methodRow, "Tests[0].MethodItems[0].Result"), StringComparison.Ordinal);
        Assert.Equal(MethodText.ToString(), HiddenValue(html, "Tests[0].MethodItems[0].TestMethodItemId"));

        Assert.Contains("This Test has no Result Items", html, StringComparison.Ordinal);
        Assert.Contains("This Test has no Method Items", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_DropdownItemsRenderAsSelectWithPleaseSelectAndRegexItemsAsTextbox()
    {
        _apiClient.ActualResults = Page();

        var html = await GetHtmlAsync();

        var growth = SelectTag(html, "Tests[0].Samples[1].Values[0].Result");
        Assert.NotNull(growth);
        Assert.Contains("<option value=\"\">Please Select</option>", growth, StringComparison.Ordinal);
        Assert.Contains("<option value=\"Positive\">Positive</option>", growth, StringComparison.Ordinal);
        Assert.Contains("<option value=\"Negative\">Negative</option>", growth, StringComparison.Ordinal);

        var selected = SelectTag(html, "Tests[0].Samples[0].Values[0].Result");
        Assert.Matches("<option selected=\"selected\" value=\"Positive\">", selected);

        var medium = SelectTag(html, "Tests[0].MethodItems[1].Result");
        Assert.Contains("<option value=\"\">Please Select</option>", medium, StringComparison.Ordinal);
        Assert.Matches("<option selected=\"selected\" value=\"Broth\">", medium);

        var count = InputTag(html, "Tests[0].Samples[0].Values[1].Result");
        Assert.NotNull(count);
        Assert.Contains("value=\"12\"", count, StringComparison.Ordinal);
        Assert.Null(SelectTag(html, "Tests[0].Samples[0].Values[1].Result"));
        Assert.Null(InputTag(html, "Tests[0].Samples[0].Values[0].Result"));
    }

    [Fact]
    public async Task Get_InputsHaveVisuallyHiddenLabelsAndRegexHintsLinkedByAriaDescribedBy()
    {
        _apiClient.ActualResults = Page();

        var html = await GetHtmlAsync();

        Assert.Contains("<label class=\"govuk-label govuk-visually-hidden\" for=\"Tests_0__Samples_0__Values_1__Result\">Count for sample S001</label>", html, StringComparison.Ordinal);
        Assert.Contains("<label class=\"govuk-label govuk-visually-hidden\" for=\"Tests_0__Samples_0__Values_0__Result\">Growth for sample S001</label>", html, StringComparison.Ordinal);

        var columnHint = Regex.Match(html, "<div id=\"([^\"]+)\" class=\"govuk-hint[^\"]*\">Input must be of format: Whole number</div>");
        Assert.True(columnHint.Success);
        Assert.Contains($"aria-describedby=\"{columnHint.Groups[1].Value}\"", InputTag(html, "Tests[0].Samples[1].Values[1].Result"), StringComparison.Ordinal);

        var methodHint = Regex.Match(html, "<div id=\"([^\"]+)\" class=\"govuk-hint[^\"]*\">Input must be of format: Text</div>");
        Assert.True(methodHint.Success);
        Assert.Contains($"aria-describedby=\"{methodHint.Groups[1].Value}\"", InputTag(html, "Tests[0].MethodItems[0].Result"), StringComparison.Ordinal);

        Assert.DoesNotContain("Input must be of format: Positive or Negative", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_PostsTheFormToActualResultsWithRouteValuesAndHiddenIds()
    {
        _apiClient.ActualResults = Page();

        var html = await GetHtmlAsync();

        Assert.Matches($"<form method=\"post\" action=\"/Distribution/ActualResults\\?year=2026&amp;monthId=5&amp;schemeId={Scheme}&amp;participantId={Participant}\">", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Equal("2026", HiddenValue(html, "YearId"));
        Assert.Equal("5", HiddenValue(html, "MonthId"));
        Assert.Equal(Scheme.ToString(), HiddenValue(html, "MonthlyDistributionSchemeId"));
        Assert.Equal(Participant.ToString(), HiddenValue(html, "DistributionParticipantId"));
        Assert.Equal("False", HiddenValue(html, "IsAuditStep"));
        Assert.Equal(Value12.ToString(), HiddenValue(html, "Tests[0].Samples[0].Values[1].SampleResultItemId"));
        Assert.Equal(TextItem.ToString(), HiddenValue(html, "Tests[0].Samples[0].Values[1].TestResultItemId"));
    }

    [Fact]
    public async Task Get_ApiReturnsNull_Returns404()
    {
        _apiClient.ActualResults = null;

        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Save_SendsEveryValueToTheApi()
    {
        _apiClient.ActualResults = Page();
        _apiClient.ResultsReceipt = new ResultsReceiptResponse(Guid.NewGuid(), 2026, 5, []);
        _apiClient.ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>());

        var response = await PostAsync("save");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = Assert.Single(_apiClient.SavedActualResultsRequests);
        Assert.Equal(
            [
                new ActualResultsSampleValueRequest(Value11, "Positive"),
                new ActualResultsSampleValueRequest(Value12, "12"),
                new ActualResultsSampleValueRequest(Value21, "Negative"),
                new ActualResultsSampleValueRequest(Value22, "7"),
            ],
            request.SampleValues);
        Assert.Equal(
            [new ActualResultsMethodItemRequest(MethodText, "37C"), new ActualResultsMethodItemRequest(MethodDropdown, "Broth")],
            request.MethodItems);
    }

    [Fact]
    public async Task Post_Cancel_DoesNotCallTheApi()
    {
        _apiClient.ActualResults = Page();
        _apiClient.ResultsReceipt = new ResultsReceiptResponse(Guid.NewGuid(), 2026, 5, []);

        var response = await PostAsync("cancel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Post_AuditReasonRequired_ShowsAuditStepWithRoundTrippedValues()
    {
        _apiClient.ActualResults = Page();
        _apiClient.ActualResultsSaveResult = new ActualResultsSaveResult(
            false, new Dictionary<Guid, string>(), AuditReasonRequired: true, AuditObjectName: "Results for L001: Lab One");

        var response = await PostAsync("save", FormFields(sample1Count: "99"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"Audit Trail - {Heading}", Title(html), StringComparison.Ordinal);
        Assert.Equal("Audit Trail", H1(html));
        Assert.Contains("Changes have been made to the following object:", html, StringComparison.Ordinal);
        Assert.Contains(">Results for L001: Lab One</h2>", html, StringComparison.Ordinal);
        Assert.Contains("Before these modifications can be saved, you must enter your reasons for making these changes below:", WebUtility.HtmlDecode(html), StringComparison.Ordinal);

        var textarea = Regex.Match(html, "<textarea[^>]*>").Value;
        Assert.Contains("name=\"AuditWhy\"", textarea, StringComparison.Ordinal);
        Assert.Contains("rows=\"5\"", textarea, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"2000\"", textarea, StringComparison.Ordinal);

        var buttons = Regex.Matches(html, "<button type=\"submit\" name=\"command\" value=\"(\\w+)\"[^>]*>(.*?)</button>", RegexOptions.Singleline)
            .Select(m => (Value: m.Groups[1].Value, Label: Text(m.Groups[2].Value)))
            .ToList();
        Assert.Equal([("save", "Save"), ("cancelAudit", "Cancel")], buttons);

        Assert.Equal("true", HiddenValue(html, "IsAuditStep"));
        Assert.Equal("Results for L001: Lab One", HiddenValue(html, "AuditObjectName"));
        Assert.Equal("save", HiddenValue(html, "PendingCommand"));
        Assert.Equal("2026", HiddenValue(html, "YearId"));
        Assert.Equal("5", HiddenValue(html, "MonthId"));
        Assert.Equal(Scheme.ToString(), HiddenValue(html, "MonthlyDistributionSchemeId"));
        Assert.Equal(Participant.ToString(), HiddenValue(html, "DistributionParticipantId"));
        Assert.Equal(TestId.ToString(), HiddenValue(html, "Tests[0].TestId"));
        Assert.Equal(TextItem.ToString(), HiddenValue(html, "Tests[0].ResultItems[1].TestResultItemId"));
        Assert.Equal(Sample2.ToString(), HiddenValue(html, "Tests[0].Samples[1].SampleId"));
        Assert.Equal(Value12.ToString(), HiddenValue(html, "Tests[0].Samples[0].Values[1].SampleResultItemId"));
        Assert.Equal(TextItem.ToString(), HiddenValue(html, "Tests[0].Samples[0].Values[1].TestResultItemId"));
        Assert.Equal("99", HiddenValue(html, "Tests[0].Samples[0].Values[1].Result"));
        Assert.Equal("Positive", HiddenValue(html, "Tests[0].Samples[0].Values[0].Result"));
        Assert.Equal(MethodText.ToString(), HiddenValue(html, "Tests[0].MethodItems[0].TestMethodItemId"));
        Assert.Equal("37C", HiddenValue(html, "Tests[0].MethodItems[0].Result"));
        Assert.DoesNotContain("govuk-input", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_AuditStepWithoutReason_ShowsTheReasonRequiredError()
    {
        _apiClient.ActualResults = Page();
        _apiClient.ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>());

        var response = await PostAsync("save", [.. FormFields().Where(f => f.Name != "IsAuditStep"), ("IsAuditStep", "true"), ("PendingCommand", "save")]);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The reason for these changes must be specified", html, StringComparison.Ordinal);
        Assert.Contains("govuk-error-summary", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"#AuditWhy\">", html, StringComparison.Ordinal);
        Assert.Empty(_apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public async Task Post_DeadlinePassed_ShowsTheSaveFailedMessageInTheErrorSummary()
    {
        _apiClient.ActualResults = Page();
        _apiClient.ActualResultsSaveResult = new ActualResultsSaveResult(false, new Dictionary<Guid, string>(), DeadlinePassed: true);

        var response = await PostAsync("save");

        var html = await response.Content.ReadAsStringAsync();
        var summary = Regex.Match(html, "<div class=\"govuk-error-summary\".*?</ul>", RegexOptions.Singleline).Value;
        Assert.Contains("Save Failed: No results can be saved after the deadline date.", summary, StringComparison.Ordinal);
        Assert.StartsWith("Error: ", Title(html), StringComparison.Ordinal);
        Assert.Equal("Results", H1(html));
    }

    [Fact]
    public async Task Post_InvalidValue_ShowsFieldErrorSummaryInlineErrorAndKeepsTheTypedValue()
    {
        _apiClient.ActualResults = Page();
        _apiClient.ActualResultsSaveResult = new ActualResultsSaveResult(
            false, new Dictionary<Guid, string> { [Value12] = "Input must be of format: Whole number" });

        var response = await PostAsync("save", FormFields(sample1Count: "abc"));

        var html = await response.Content.ReadAsStringAsync();
        var summary = Regex.Match(html, "<div class=\"govuk-error-summary\".*?</ul>", RegexOptions.Singleline).Value;
        Assert.Contains("<a href=\"#Tests_0__Samples_0__Values_1__Result\">Input must be of format: Whole number</a>", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Save Failed", html, StringComparison.Ordinal);

        Assert.Contains("govuk-form-group--error", html, StringComparison.Ordinal);
        Assert.Contains("<p id=\"Tests_0__Samples_0__Values_1__Result-error\" class=\"govuk-error-message\">", html, StringComparison.Ordinal);

        var input = InputTag(html, "Tests[0].Samples[0].Values[1].Result");
        Assert.Contains("govuk-input--error", input, StringComparison.Ordinal);
        Assert.Contains("value=\"abc\"", input, StringComparison.Ordinal);
        Assert.Contains("Tests_0__Samples_0__Values_1__Result-error", input, StringComparison.Ordinal);
        Assert.Contains("actual-results-hint-0-1", input, StringComparison.Ordinal);
    }
}
