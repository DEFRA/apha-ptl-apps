using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/Schedule.cshtml through the real view engine - legacy
// Scheduling/monthlys.aspx parity for the Save/Apply/Cancel button bar and the month/year heading.
public partial class ScheduleRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public ScheduleRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private static MonthlyDistributionResponse Schedule(int year, int monthId) =>
        new(Guid.NewGuid(), year, monthId,
        [
            new MonthlyDistributionSchemeResponse(SchemeId, Guid.NewGuid(), "PT0060", "Mastitis bovine", "17152A/BA",
                new DateTime(year, monthId, 1), new DateTime(year, monthId, 1), new DateTime(year, monthId, 5), new DateTime(year, monthId, 10),
                ParticipantCount: 3, TotalSetsOfSamplesRequired: 5, HasSampleNumbersDefined: true, HasIntendedResults: false,
                IsCancelled: false, IsAsAvailable: false),
        ]);

    private async Task<string> GetHtmlAsync(int year = 2026, int monthId = 4)
    {
        _apiClient.Schedule = Schedule(year, monthId);

        var response = await _factory.CreateClient().GetAsync($"/Distribution/Schedule?year={year}&monthId={monthId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string ButtonGroup(string html) =>
        Regex.Match(html, "<div class=\"govuk-button-group\">.*?</div>", RegexOptions.Singleline).Value;

    private static string CancelHref(string html) =>
        Regex.Match(ButtonGroup(html), "<a [^>]*href=\"([^\"]+)\"[^>]*>Cancel</a>").Groups[1].Value;

    // Legacy monthlys.aspx: ButtonSave, ButtonApply, ButtonCancel, in that order.
    [Fact]
    public async Task Get_RendersSaveApplyAndCancelInLegacyOrder()
    {
        var html = await GetHtmlAsync();

        var buttons = ButtonGroup(html);
        Assert.Contains("name=\"command\" value=\"save\"", buttons, StringComparison.Ordinal);
        Assert.Contains("name=\"command\" value=\"apply\"", buttons, StringComparison.Ordinal);

        var labels = Regex.Matches(buttons, ">(Save|Apply|Cancel)<").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["Save", "Apply", "Cancel"], labels);
    }

    // Apply posts the same form as Save, so it must be a plain submit that runs validation
    // (legacy ButtonApply has CausesValidation=True and the same "Dates" ValidationGroup).
    [Fact]
    public async Task Get_ApplyIsAValidatingSubmitOnTheMainForm()
    {
        var html = await GetHtmlAsync();

        var apply = Regex.Match(html, "<button[^>]*value=\"apply\"[^>]*>").Value;
        Assert.Contains("type=\"submit\"", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("formnovalidate", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("formaction", apply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_RendersTheMonthYearHeading()
    {
        var html = await GetHtmlAsync();

        Assert.Contains("Monthly Distributions - Apr 2026</h1>", html, StringComparison.Ordinal);
    }

    // The Dashboard is keyed by financial year, so a January month has to return to the year before.
    [Theory]
    [InlineData(2026, 4, 2026)]
    [InlineData(2027, 1, 2026)]
    public async Task Get_CancelReturnsToTheSelectedFinancialYear(int year, int monthId, int expectedYear)
    {
        var html = await GetHtmlAsync(year, monthId);

        Assert.Matches($"^/Distribution(/Index)?\\?year={expectedYear}$", CancelHref(html));
    }

    [Fact]
    public async Task Post_ValidationFailure_KeepsTheMonthYearHeading()
    {
        var client = _factory.CreateClient();
        _apiClient.Schedule = Schedule(2027, 1);
        var form = await client.GetAsync("/Distribution/Schedule?year=2027&monthId=1");
        var token = AntiforgeryTokenPattern().Match(await form.Content.ReadAsStringAsync()).Groups[1].Value;

        var response = await client.PostAsync(
            "/Distribution/Schedule?year=2027&monthId=1",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["command"] = "apply",
                ["Schemes[0].MonthlyDistributionSchemeId"] = SchemeId.ToString(),
                ["Schemes[0].SchemeIdentifier"] = "PT0060",
                ["Schemes[0].SchemeName"] = "Mastitis bovine",
                ["Schemes[0].DistributionDate"] = "01/01/2027",
                ["Schemes[0].OverseasPostingDate"] = "01/01/2027",
                ["Schemes[0].DeadlineDate"] = string.Empty,
                ["Schemes[0].ResultsIssueTargetDate"] = "10/01/2027",
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Monthly Distributions - Jan 2027</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Deadline Date required.", html, StringComparison.Ordinal);
        Assert.Matches("^/Distribution(/Index)?\\?year=2026$", CancelHref(html));
    }

    [Fact]
    public async Task Post_CancelCommence_KeepsTheMonthYearHeading()
    {
        var client = _factory.CreateClient();
        _apiClient.Schedule = Schedule(2026, 4);
        var form = await client.GetAsync("/Distribution/Schedule?year=2026&monthId=4");
        var token = AntiforgeryTokenPattern().Match(await form.Content.ReadAsStringAsync()).Groups[1].Value;

        var response = await client.PostAsync(
            $"/Distribution/ToggleCancelled?year=2026&monthId=4&schemeId={SchemeId}&confirmation=confirm",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Schemes[0].MonthlyDistributionSchemeId"] = SchemeId.ToString(),
                ["Schemes[0].SchemeIdentifier"] = "PT0060",
                ["Schemes[0].SchemeName"] = "Mastitis bovine",
                ["Schemes[0].IsCancelled"] = "false",
                ["Schemes[0].DistributionDate"] = "01/04/2026",
                ["Schemes[0].OverseasPostingDate"] = "01/04/2026",
                ["Schemes[0].DeadlineDate"] = "05/04/2026",
                ["Schemes[0].ResultsIssueTargetDate"] = "10/04/2026",
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Monthly Distributions - Apr 2026</h1>", html, StringComparison.Ordinal);
        Assert.Contains("[Cancelled] PT0060: Mastitis bovine", html, StringComparison.Ordinal);
    }
}
