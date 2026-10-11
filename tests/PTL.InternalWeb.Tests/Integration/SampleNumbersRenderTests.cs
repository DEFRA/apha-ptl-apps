using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/SampleNumbers.cshtml through the real view engine so the
// per-test tab layout (legacy PopulateTabContainer parity) and the audit-reason step (legacy
// Audit.aspx parity) are exercised as markup, not just as controller return values.
public partial class SampleNumbersRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    [GeneratedRegex("class=\"govuk-tabs__tab\"[^>]*>([^<]*)<", RegexOptions.None)]
    private static partial Regex TabLabelPattern();

    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SampleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public SampleNumbersRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private static Guid TestIdFor(int index) => Guid.Parse($"44444444-4444-4444-4444-{index:D12}");

    private static Guid SampleTestIdFor(int index) => Guid.Parse($"33333333-3333-3333-3333-{index:D12}");

    private static SampleNumbersResponse Response(int testCount, bool requiresAssessment = false) => new(
        SchemeId,
        "17152A/BA",
        "PT0060",
        "Mastitis bovine",
        9,
        "Server comments",
        requiresAssessment,
        false,
        [
            new SampleNumberRowResponse(SampleId, "S1", "Internal 1", "External 1", "Tabulation 1", "TC 1",
                [.. Enumerable.Range(0, testCount).Select(i => new SampleAllocationResponse(SampleTestIdFor(i), TestIdFor(i), i % 2 == 0))]),
        ],
        [.. Enumerable.Range(0, testCount).Select(i => new SampleNumbersTestResponse(
            TestIdFor(i), $"Test {i}", [$"Result {i}"], [$"Method {i}"], [$"Category {i}"]))]);

    private async Task<string> GetSampleNumbersHtmlAsync(int testCount, bool requiresAssessment = false)
    {
        _apiClient.SampleNumbers = Response(testCount, requiresAssessment);

        var response = await _factory.CreateClient()
            .GetAsync($"/Distribution/SampleNumbers?year=2026&monthId=4&schemeId={SchemeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public async Task TabList_RendersOneTabPerTest_LabelledWithTheTestName(int testCount)
    {
        var html = await GetSampleNumbersHtmlAsync(testCount);

        var labels = TabLabelPattern().Matches(html).Select(m => m.Groups[1].Value.Trim()).ToList();

        Assert.Equal(testCount, labels.Count);
        Assert.Equal([.. Enumerable.Range(0, testCount).Select(i => $"Test {i}")], labels);
        for (var i = 0; i < testCount; i++)
        {
            Assert.Contains($"id=\"test-{TestIdFor(i)}\"", html, StringComparison.Ordinal);
        }
    }

    // Legacy's header row per table IS the section label (rowPrepSheetHeader), so the label must
    // sit in a real <th> - that is both legacy-faithful and what the accessibility rules require.
    // What must never come back is an EMPTY header band, i.e. a <th> holding only hidden text.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public async Task ItemTables_LabelTheirHeaderRowWithTheSectionName(int testCount)
    {
        var html = await GetSampleNumbersHtmlAsync(testCount, requiresAssessment: true);

        Assert.DoesNotContain("<span class=\"govuk-visually-hidden\">Test Item</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<span class=\"govuk-visually-hidden\">Method Item</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<span class=\"govuk-visually-hidden\">Category/Criterion Item</span>", html, StringComparison.Ordinal);

        foreach (var section in new[] { "Test Items:", "Method Items:", "Category/Criterion Items: " })
        {
            Assert.Equal(testCount, Regex.Count(html, $"<th scope=\"col\" class=\"govuk-table__header\">{Regex.Escape(section)}</th>"));
            Assert.DoesNotContain($"govuk-table__caption--s\">{section}<", html, StringComparison.Ordinal);
        }

        // The Samples: table is a real data table and keeps its caption plus its own column headers.
        Assert.Contains("govuk-table__caption--s\">Samples:<", html, StringComparison.Ordinal);

        // Legacy's 15px (spacing scale 3) gap between the stacked tables is unchanged.
        Assert.Equal(testCount * 4, Regex.Count(html, "class=\"govuk-table govuk-!-margin-bottom-3\""));
    }

    [Fact]
    public async Task AuditStep_RendersLegacyWordingAndCarriesEveryPendingEditForward()
    {
        _apiClient.SampleNumbers = Response(testCount: 2);
        _apiClient.SampleNumbersSaveResult = new SampleNumbersSaveResult(
            false, [], new Dictionary<Guid, SampleNumbersFieldError[]>(), AuditReasonRequired: true, AuditObjectName: "Intended Results Set");

        var client = _factory.CreateClient();
        var form = await client.GetAsync($"/Distribution/SampleNumbers?year=2026&monthId=4&schemeId={SchemeId}");
        var token = AntiforgeryTokenPattern().Match(await form.Content.ReadAsStringAsync()).Groups[1].Value;

        var response = await client.PostAsync(
            $"/Distribution/SampleNumbers?year=2026&monthId=4&schemeId={SchemeId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["command"] = "saveAndGoToIntendedResults",
                ["Comments"] = "User comments",
                ["Samples[0].SampleId"] = SampleId.ToString(),
                ["Samples[0].SampleNumber"] = "S1-typed",
                ["Samples[0].InternalDescription"] = "Internal typed",
                ["Samples[0].TabulationDescription"] = "Tabulation typed",
                ["Samples[0].ExternalDescription"] = "External typed",
                ["Samples[0].TestConsultantsDescription"] = "TC typed",
                ["Samples[0].Allocations[0].SampleTestId"] = SampleTestIdFor(0).ToString(),
                ["Samples[0].Allocations[0].TestId"] = TestIdFor(0).ToString(),
                ["Samples[0].Allocations[0].IsApplicable"] = "true",
                ["Samples[0].Allocations[1].SampleTestId"] = SampleTestIdFor(1).ToString(),
                ["Samples[0].Allocations[1].TestId"] = TestIdFor(1).ToString(),
                ["Samples[0].Allocations[1].IsApplicable"] = "false",
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        // Legacy Audit.aspx wording.
        Assert.Contains("Changes have been made to the following object:", html, StringComparison.Ordinal);
        Assert.Contains(">Intended Results Set</h2>", html, StringComparison.Ordinal);
        Assert.Contains("Before these modifications can be saved, you must enter your reasons for making these changes below: ", html, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"2000\"", html, StringComparison.Ordinal);

        // The audit step replaces the sample table / comments / tabs, exactly as Audit.aspx did.
        Assert.DoesNotContain("govuk-tabs", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Number of each Sample required", html, StringComparison.Ordinal);

        // Buttons: Save re-posts the pending command, Cancel leaves without saving.
        Assert.Contains("name=\"command\" value=\"saveAndGoToIntendedResults\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"command\" value=\"cancelAudit\"", html, StringComparison.Ordinal);
        Assert.Contains("formnovalidate", html, StringComparison.Ordinal);

        // Every pending edit rides along hidden - legacy kept these in Session.
        AssertHiddenInput(html, "Comments", "User comments");
        AssertHiddenInput(html, "RequiresAuditReason", "True");
        AssertHiddenInput(html, "AuditObjectName", "Intended Results Set");
        AssertHiddenInput(html, "PendingCommand", "saveAndGoToIntendedResults");
        AssertHiddenInput(html, "Samples[0].SampleId", SampleId.ToString());
        AssertHiddenInput(html, "Samples[0].SampleNumber", "S1-typed");
        AssertHiddenInput(html, "Samples[0].InternalDescription", "Internal typed");
        AssertHiddenInput(html, "Samples[0].TabulationDescription", "Tabulation typed");
        AssertHiddenInput(html, "Samples[0].ExternalDescription", "External typed");
        AssertHiddenInput(html, "Samples[0].TestConsultantsDescription", "TC typed");
        AssertHiddenInput(html, "Samples[0].Allocations[0].SampleTestId", SampleTestIdFor(0).ToString());
        AssertHiddenInput(html, "Samples[0].Allocations[0].TestId", TestIdFor(0).ToString());
        // Re-displayed values come from ModelState, so these carry the posted casing verbatim;
        // both "true" and "True" bind back to the same bool.
        AssertHiddenInput(html, "Samples[0].Allocations[0].IsApplicable", "true");
        AssertHiddenInput(html, "Samples[0].Allocations[1].SampleTestId", SampleTestIdFor(1).ToString());
        AssertHiddenInput(html, "Samples[0].Allocations[1].TestId", TestIdFor(1).ToString());
        AssertHiddenInput(html, "Samples[0].Allocations[1].IsApplicable", "false");
    }

    // Attribute order within the rendered <input> is the tag helper's business, so match the tag
    // by name and then look for the value inside that same tag.
    private static void AssertHiddenInput(string html, string name, string value)
    {
        var pattern = $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>";
        var match = Regex.Match(html, pattern);

        Assert.True(match.Success, $"No hidden input was rendered for '{name}'.");
        Assert.Contains("type=\"hidden\"", match.Value, StringComparison.Ordinal);
        Assert.Contains($"value=\"{value}\"", match.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditStep_MissingReason_ShowsTheLegacyRequiredMessageInTheErrorSummary()
    {
        _apiClient.SampleNumbers = Response(testCount: 1);

        var client = _factory.CreateClient();
        var form = await client.GetAsync($"/Distribution/SampleNumbers?year=2026&monthId=4&schemeId={SchemeId}");
        var token = AntiforgeryTokenPattern().Match(await form.Content.ReadAsStringAsync()).Groups[1].Value;

        var response = await client.PostAsync(
            $"/Distribution/SampleNumbers?year=2026&monthId=4&schemeId={SchemeId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["command"] = "save",
                ["RequiresAuditReason"] = "True",
                ["AuditObjectName"] = "Intended Results Set",
                ["PendingCommand"] = "save",
                ["Comments"] = "User comments",
                ["Samples[0].SampleId"] = SampleId.ToString(),
                ["Samples[0].SampleNumber"] = "S1-typed",
                ["Samples[0].InternalDescription"] = "Internal typed",
                ["Samples[0].TabulationDescription"] = "Tabulation typed",
                ["Samples[0].ExternalDescription"] = "External typed",
                ["Samples[0].TestConsultantsDescription"] = "TC typed",
                ["Samples[0].Allocations[0].SampleTestId"] = SampleTestIdFor(0).ToString(),
                ["Samples[0].Allocations[0].TestId"] = TestIdFor(0).ToString(),
                ["Samples[0].Allocations[0].IsApplicable"] = "false",
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("govuk-error-summary", html, StringComparison.Ordinal);
        Assert.Contains("The reason for these changes must be specified", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#AuditWhy\"", html, StringComparison.Ordinal);
    }

    // The distribution structure could not be built from its Scheme, so the page is replaced by the
    // business reason instead of the sample table (legacy threw an unhandled KeyNotFoundException).
    [Fact]
    public async Task StructureErrors_RenderTheMessagesInALinklessErrorSummaryAndOfferTheWayBack()
    {
        _apiClient.SampleNumbers = Response(testCount: 1);
        _apiClient.SampleNumbersErrors =
        [
            "Scheme PT0060 has no Test Consultant Tabulation. Set one on the Scheme before defining Sample Numbers.",
            "Results Tabulation 'Cell count' is no longer on any test for Scheme PT0060.",
        ];

        var response = await _factory.CreateClient()
            .GetAsync($"/Distribution/SampleNumbers?year=2026&monthId=4&schemeId={SchemeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>Error: Sample Numbers", html, StringComparison.Ordinal);
        Assert.Contains("data-module=\"govuk-error-summary\"", html, StringComparison.Ordinal);
        foreach (var message in _apiClient.SampleNumbersErrors)
        {
            Assert.Contains($"<li>{WebUtility.HtmlEncode(message)}</li>", html, StringComparison.Ordinal);
        }

        // Whole-page messages have no field to focus, so no summary item may be a link.
        Assert.DoesNotContain("govuk-error-summary__list\"><li><a", html, StringComparison.Ordinal);

        // The sample table is replaced, not merely annotated.
        Assert.DoesNotContain("govuk-tabs", html, StringComparison.Ordinal);

        Assert.Contains("<a class=\"govuk-back-link\" href=\"/Distribution/Schedule?year=2026&amp;monthId=4\">Back</a>", html, StringComparison.Ordinal);

        // Navigation, so a plain link rather than an anchor wearing role="button".
        Assert.Contains("<a class=\"govuk-link\" href=\"/Distribution/Schedule?year=2026&amp;monthId=4\">Return to Monthly Distributions</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"button\"", html, StringComparison.Ordinal);
    }
}
