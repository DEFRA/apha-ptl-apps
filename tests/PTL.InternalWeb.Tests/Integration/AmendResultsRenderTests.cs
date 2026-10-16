using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using Keys = PTL.Contracts.Distribution.AmendResultsFieldKeys;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/AmendResults.cshtml through the real view engine - legacy
// Scheduling/AmendResults.aspx (and its Audit.aspx reason step) parity.
public partial class AmendResultsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private static readonly Guid Scheme = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000d1");
    private static readonly Guid TestId = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000d2");
    private static readonly Guid MethodItem = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000d3");
    private static readonly Guid ResultItem = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000d4");
    private static readonly Guid CategoryItem = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000d5");
    private static readonly Guid Sample1 = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000d6");
    private static readonly Guid Sample2 = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000d7");
    private static readonly Guid WithResults = Guid.Parse("dddddddd-0000-0000-0000-0000000000d1");
    private static readonly Guid NilReturn = Guid.Parse("dddddddd-0000-0000-0000-0000000000d2");
    private static readonly Guid RatingA = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000d1");
    private static readonly Guid RatingB = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000d2");

    private static readonly string Url = $"/Distribution/AmendResults?year=2026&monthId=5&schemeId={Scheme}";

    private const string Title = "D1a/S1 BT: Brucella";
    private const string AuditObject = "Results for Distribution D1a/S1";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public AmendResultsRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private void Seed() =>
        _apiClient.AmendResults = new AmendResultsResponse(
            Scheme, Title, RequiresAssessment: true, StoreRatings: true,
            [
                new AmendTestResponse(
                    TestId, "Brucella", IsAssessmentSubmitted: true,
                    [new AmendItemResponse(MethodItem, "Method", @"^\d+$", "Number", false)],
                    [new AmendItemResponse(ResultItem, "Result", "Pos|Neg", "", false)],
                    [new AmendCategoryItemResponse(CategoryItem, "Accuracy")],
                    [new AmendSampleResponse(Sample1, "S1-001"), new AmendSampleResponse(Sample2, "S1-002")],
                    [new AmendIntendedValueResponse(MethodItem, Guid.Empty, "10", "")],
                    [new AmendIntendedValueResponse(ResultItem, Sample1, "Pos", ""), new AmendIntendedValueResponse(ResultItem, Sample2, "Neg", "")]),
            ],
            [
                new AmendParticipantResponse(
                    WithResults, "L001", "", HasResults: true, HasRatingResults: true,
                    [new AmendParticipantValueResponse(TestId, MethodItem, Guid.Empty, "12")],
                    [new AmendParticipantValueResponse(TestId, ResultItem, Sample1, "Pos"), new AmendParticipantValueResponse(TestId, ResultItem, Sample2, "Neg")],
                    [new AmendParticipantScoreResponse(TestId, CategoryItem, Sample1, 3), new AmendParticipantScoreResponse(TestId, CategoryItem, Sample2, 4)],
                    [new AmendAssessmentResponse(TestId, Sample1, "Good", 5)],
                    [new AmendRatingResponse(Sample1, RatingA)]),
                new AmendParticipantResponse(NilReturn, "L002", "Not received", HasResults: false, HasRatingResults: false, [], [], [], [], []),
            ],
            [new AmendRatingOptionResponse(RatingA, "Satisfactory"), new AmendRatingOptionResponse(RatingB, "Unsatisfactory")],
            [new AmendSampleResponse(Sample1, "S1-001")],
            [new AmendSampleStatisticsResponse(Sample1, "4.50", "3", "10", "75%")]);

    private async Task<string> GetHtmlAsync()
    {
        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> PostHtmlAsync(string command, params (string Name, string Value)[] fields)
    {
        var client = _factory.CreateClient();
        var form = await client.GetAsync(Url);
        var token = AntiforgeryTokenPattern().Match(await form.Content.ReadAsStringAsync()).Groups[1].Value;

        var content = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token), new("command", command) };
        content.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));

        var response = await client.PostAsync(Url, new FormUrlEncodedContent(content));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Text(string fragment) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim());

    private static string PageTitle(string html) => Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value;

    private static string H1(string html) => Text(Regex.Match(html, "<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value);

    private static string H2(string html) => Text(Regex.Match(html, "<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline).Groups[1].Value);

    private static string MainContent(string html) => Regex.Match(html, "<main.*?</main>", RegexOptions.Singleline).Value;

    private static bool HasCommandButton(string html, string value, string label) =>
        Regex.IsMatch(html, $"<button[^>]*name=\"command\"[^>]*value=\"{value}\"[^>]*>{label}</button>");

    private static bool HasInput(string html, string name) => html.Contains($"name=\"{name}\"", StringComparison.Ordinal);

    [Fact]
    public async Task Get_ApiReturnsNull_Returns404()
    {
        _apiClient.AmendResults = null;

        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_RendersLegacyTitleHeadingAndIntro()
    {
        Seed();

        var html = await GetHtmlAsync();

        Assert.Contains("Amend Results", WebUtility.HtmlDecode(PageTitle(html)), StringComparison.Ordinal);
        Assert.DoesNotContain("Audit Trail", PageTitle(html), StringComparison.Ordinal);
        Assert.Equal("Amend Results", H1(html));
        Assert.Equal(Title, H2(html));
        Assert.Contains(
            "All of the Intended Results and the Results received from Participants are shown below for Amending.",
            Text(MainContent(html)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_BackLinkReturnsToTheTabulationsForTheSameMonth()
    {
        Seed();

        var back = Regex.Match(await GetHtmlAsync(), "<a class=\"govuk-back-link[^\"]*\" href=\"([^\"]+)\">Back</a>");

        Assert.True(back.Success);
        Assert.Equal("/Distribution/Tabulations?year=2026&monthId=5", WebUtility.HtmlDecode(back.Groups[1].Value));
    }

    [Fact]
    public async Task Get_RendersAnInputForEveryAmendableValue()
    {
        Seed();

        var html = await GetHtmlAsync();

        Assert.True(HasInput(html, Keys.IntendedMethod(MethodItem)));
        Assert.True(HasInput(html, Keys.IntendedResult(Sample1, ResultItem, 1)));
        Assert.True(HasInput(html, Keys.IntendedResult(Sample2, ResultItem, 2)));
        Assert.True(HasInput(html, Keys.ParticipantMethod(WithResults, MethodItem)));
        Assert.True(HasInput(html, Keys.ParticipantResult(WithResults, Sample1, ResultItem)));
        Assert.True(HasInput(html, Keys.CategoryScore(WithResults, Sample2, CategoryItem)));
        Assert.True(HasInput(html, Keys.AssessmentComment(WithResults, TestId, Sample1)));
        Assert.True(HasInput(html, Keys.AssessmentScore(WithResults, TestId, Sample1)));
        Assert.True(HasInput(html, Keys.NilReturn(NilReturn)));
        Assert.True(HasInput(html, Keys.Rating(WithResults, Sample1)));
        Assert.Matches($"<input[^>]*name=\"{Keys.Range(ResultItem)}\"[^>]*type=\"checkbox\"", html);
        Assert.Matches($"<input type=\"hidden\" name=\"{Keys.Range(ResultItem)}_shown\" value=\"1\"", html);

        // Nothing is rendered for an assessment that does not exist or a participant without a nil return.
        Assert.False(HasInput(html, Keys.AssessmentComment(WithResults, TestId, Sample2)));
        Assert.False(HasInput(html, Keys.NilReturn(WithResults)));
        Assert.False(HasInput(html, Keys.ParticipantMethod(NilReturn, MethodItem)));
    }

    [Fact]
    public async Task Get_PrefillsStoredValuesAndShowsTheRegexFormatHint()
    {
        Seed();

        var html = await GetHtmlAsync();

        Assert.Matches($"<input[^>]*id=\"{Keys.IntendedMethod(MethodItem)}\"[^>]*value=\"10\"", html);
        Assert.Matches($"<input[^>]*id=\"{Keys.ParticipantMethod(WithResults, MethodItem)}\"[^>]*value=\"12\"", html);
        Assert.Matches($"<input[^>]*id=\"{Keys.NilReturn(NilReturn)}\"[^>]*value=\"Not received\"", html);
        Assert.Contains("Input must be of format: Number", Text(html), StringComparison.Ordinal);

        var result = Regex.Match(html, $"<select[^>]*name=\"{Keys.ParticipantResult(WithResults, Sample2, ResultItem)}\".*?</select>", RegexOptions.Singleline).Value;
        Assert.Matches("<option value=\"Neg\" selected", result);
        Assert.Contains("<option value=\"Pos\">", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_AssessmentScheme_ShowsTheCategoryAndAssessmentHeaders()
    {
        Seed();

        var text = Text(MainContent(await GetHtmlAsync()));

        Assert.Contains("Categories", text, StringComparison.Ordinal);
        Assert.Contains("Assessment (Comments / Overall score)", text, StringComparison.Ordinal);
        Assert.Contains("Accuracy", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_StoreRatings_ShowsTheQauRatingsTableWithTheStatistics()
    {
        Seed();

        var html = await GetHtmlAsync();

        var ratings = Regex.Match(html, "<caption class=\"govuk-visually-hidden\">QAU Ratings</caption>.*?</table>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(ratings);
        var text = Text(ratings);
        Assert.Contains("Mean score of all laboratories: 4.50", text, StringComparison.Ordinal);
        Assert.Contains("No. of labs reporting intended result: 3", text, StringComparison.Ordinal);
        Assert.Contains("No. of Participants: 10", text, StringComparison.Ordinal);
        Assert.Contains("Percent Correct: 75%", text, StringComparison.Ordinal);
        Assert.Matches($"<option value=\"{RatingA}\" selected[^>]*>Satisfactory</option>", ratings);
        Assert.Contains($"<option value=\"{RatingB}\">Unsatisfactory</option>", ratings, StringComparison.Ordinal);
        Assert.Contains("data-amend-recalculate", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_RendersSaveAndCancelCommandButtonsOnlyAndTheScript()
    {
        Seed();

        var html = await GetHtmlAsync();

        Assert.True(HasCommandButton(html, "save", "Save"));
        Assert.True(HasCommandButton(html, "cancel", "Cancel"));
        Assert.False(HasCommandButton(html, "apply", "Apply"));
        Assert.DoesNotContain(">Apply</button>", html, StringComparison.Ordinal);
        Assert.Matches("<script src=\"/js/amend-results\\.js(\\?v=[^\"]+)?\"></script>", html);
        var form = Regex.Match(html, "<form[^>]*data-amend-results[^>]*>").Value;
        Assert.Contains("method=\"post\"", form, StringComparison.Ordinal);
        Assert.Contains($"action=\"/Distribution/AmendResults?year=2026&amp;monthId=5&amp;schemeId={Scheme}\"", form, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("apply")]
    public async Task Post_AuditReasonRequired_RendersTheAuditStep(string command)
    {
        Seed();
        _apiClient.AmendResultsSaveResult = new AmendResultsSaveResult(false, true, AuditObject, new Dictionary<string, string>());

        var html = await PostHtmlAsync(command, (Keys.ParticipantResult(WithResults, Sample1, ResultItem), "Neg"));

        Assert.Contains("Audit Trail - Amend Results", WebUtility.HtmlDecode(PageTitle(html)), StringComparison.Ordinal);
        Assert.Equal("Audit Trail", H1(html));
        Assert.Contains(AuditObject, Text(MainContent(html)), StringComparison.Ordinal);
        Assert.Matches("<textarea[^>]*name=\"AuditWhy\"", html);
        Assert.Matches($"<input type=\"hidden\" name=\"pendingCommand\" value=\"{command}\"", html);
        Assert.Matches("<input type=\"hidden\" name=\"isAuditStep\" value=\"true\"", html);
        Assert.True(HasCommandButton(html, command, "Save"));
        Assert.True(HasCommandButton(html, "cancelAudit", "Cancel"));
        Assert.False(HasCommandButton(html, "apply", "Apply"));
        Assert.Matches($"<input type=\"hidden\" name=\"{Keys.ParticipantResult(WithResults, Sample1, ResultItem)}\" value=\"Neg\"", html);
        Assert.DoesNotContain("<select", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_AuditStepWithoutAReason_ShowsTheReasonError()
    {
        Seed();

        var html = await PostHtmlAsync("save", ("isAuditStep", "true"), ("pendingCommand", "save"), ("auditObjectName", AuditObject));

        Assert.Equal("Audit Trail", H1(html));
        Assert.Contains("The reason for these changes must be specified", Text(MainContent(html)), StringComparison.Ordinal);
        Assert.Empty(_apiClient.SavedAmendResults);
    }

    [Fact]
    public async Task Post_FieldError_IsShownAgainstItsInput()
    {
        Seed();
        var key = Keys.ParticipantResult(WithResults, Sample1, ResultItem);
        _apiClient.AmendResultsSaveResult = new AmendResultsSaveResult(false, false, string.Empty, new Dictionary<string, string> { [key] = "Result is not valid" });

        var html = await PostHtmlAsync("save", (key, "Neg"));

        Assert.Equal("Amend Results", H1(html));
        Assert.Matches($"<p id=\"{key}-error\" class=\"govuk-error-message[^\"]*\"><span class=\"govuk-visually-hidden\">Error:</span> Result is not valid</p>", html);
        Assert.Matches($"<select class=\"govuk-select govuk-select--error\" id=\"{key}\"", html);
        Assert.Contains("govuk-error-summary", html, StringComparison.Ordinal);
        Assert.Matches("<option value=\"Neg\" selected", Regex.Match(html, $"<select[^>]*id=\"{key}\".*?</select>", RegexOptions.Singleline).Value);
    }

    [Fact]
    public async Task Post_Saved_RedirectsToTabulations()
    {
        Seed();
        _apiClient.AmendResultsSaveResult = new AmendResultsSaveResult(true, false, string.Empty, new Dictionary<string, string>());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = AntiforgeryTokenPattern().Match(await (await client.GetAsync(Url)).Content.ReadAsStringAsync()).Groups[1].Value;

        var response = await client.PostAsync(Url, new FormUrlEncodedContent(
            [new("__RequestVerificationToken", token), new("command", "save"), new(Keys.IntendedMethod(MethodItem), "11")]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Distribution/Tabulations?year=2026&monthId=5", response.Headers.Location!.OriginalString);
        Assert.Equal("11", Assert.Single(Assert.Single(_apiClient.SavedAmendResults).IntendedMethodResults).Value);
    }
}
