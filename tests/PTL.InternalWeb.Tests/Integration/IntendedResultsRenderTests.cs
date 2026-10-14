using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/IntendedResults.cshtml - legacy Scheduling/IntendedResults.aspx
// tooltip text shown as GDS hint text: free-text boxes get "Input format: <ReturnValueType>", dropdowns none.
public class IntendedResultsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid Scheme = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid TestId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid DropdownItem = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TextItem = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid Sample1 = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid Value1 = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid Value2 = Guid.Parse("33333333-0000-0000-0000-000000000002");
    private static readonly Guid MethodText = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid MethodDropdown = Guid.Parse("44444444-0000-0000-0000-000000000002");

    private static readonly string Url = $"/Distribution/IntendedResults?year=2026&monthId=5&schemeId={Scheme}";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public IntendedResultsRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private static IntendedResultsResponse Page(bool isRange = true) =>
        new(Scheme, "D1a", "S1", "Brucella", RequiresAuditReason: false,
        [
            new IntendedResultsTestResponse(TestId, "Culture test",
                [
                    new IntendedResultsResultItemResponse(DropdownItem, "Growth", "Positive|Negative", "Positive or Negative", isRange),
                    new IntendedResultsResultItemResponse(TextItem, "Count", "^\\d+$", "Whole number", isRange),
                ],
                [
                    new IntendedResultsMethodItemResponse(MethodText, "Incubation", "^.+$", "Free text", "37C"),
                    new IntendedResultsMethodItemResponse(MethodDropdown, "Medium", "Agar|Broth", "Agar or Broth", "Broth"),
                ],
                [
                    new IntendedResultsSampleRowResponse(Sample1, "S001", "First sample", IsApplicable: true,
                    [
                        new IntendedResultsSampleResultValueResponse(Value1, DropdownItem, "Positive", "Negative"),
                        new IntendedResultsSampleResultValueResponse(Value2, TextItem, "10", "20"),
                    ]),
                ]),
        ]);

    private async Task<string> GetHtmlAsync()
    {
        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static List<string> InputsNamed(string html, string name) =>
        [.. Regex.Matches(html, "<input[^>]*>").Select(m => m.Value)
            .Where(t => t.Contains($"name=\"{name}\"", StringComparison.Ordinal) && !t.Contains("type=\"hidden\"", StringComparison.Ordinal))];

    private static string? SelectNamed(string html, string name) =>
        Regex.Matches(html, "<select[^>]*>").Select(m => m.Value)
            .FirstOrDefault(t => t.Contains($"name=\"{name}\"", StringComparison.Ordinal));

    [Fact]
    public async Task Get_FreeTextResultItem_ShowsTheLegacyInputFormatAsHintTextLinkedToBothRangeBoxes()
    {
        _apiClient.IntendedResults = Page();

        var html = await GetHtmlAsync();

        Assert.Matches("<div id=\"intended-results-hint-0-1\" class=\"govuk-hint[^\"]*\">Input format: Whole number</div>", html);
        var from = Assert.Single(InputsNamed(html, "Tests[0].Samples[0].Values[1].IntendedResult"));
        var to = Assert.Single(InputsNamed(html, "Tests[0].Samples[0].Values[1].IntendedResult2"));
        Assert.Contains("aria-describedby=\"intended-results-hint-0-1\"", from, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"intended-results-hint-0-1\"", to, StringComparison.Ordinal);
        Assert.DoesNotContain("title=", from + to, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_FreeTextMethodItem_ShowsTheLegacyInputFormatAsHintTextLinkedToTheBox()
    {
        _apiClient.IntendedResults = Page();

        var html = await GetHtmlAsync();

        Assert.Matches("<div id=\"intended-results-hint-0-m0\" class=\"govuk-hint[^\"]*\">Input format: Free text</div>", html);
        var method = Assert.Single(InputsNamed(html, "Tests[0].MethodItems[0].IntendedResult"));
        Assert.Contains("aria-describedby=\"intended-results-hint-0-m0\"", method, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_Dropdowns_HaveNoInputFormatHintAsInLegacy()
    {
        _apiClient.IntendedResults = Page();

        var html = await GetHtmlAsync();

        Assert.DoesNotContain("intended-results-hint-0-0\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("intended-results-hint-0-m1\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Input format: Positive or Negative", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Input format: Agar or Broth", html, StringComparison.Ordinal);
        Assert.DoesNotContain("title=", SelectNamed(html, "Tests[0].Samples[0].Values[0].IntendedResult")!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_RangeCheckbox_ShowsItsLegacyTooltipTextAsHintTextLinkedToTheBox()
    {
        _apiClient.IntendedResults = Page();

        var html = await GetHtmlAsync();

        Assert.Matches("<div id=\"intended-results-range-hint-0-0\" class=\"govuk-hint govuk-checkboxes__hint[^\"]*\">Click here to set whether the Intended Result for this Result Item is a Range of values</div>", html);
        Assert.Contains("aria-describedby=\"intended-results-range-hint-0-0\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("title=", html, StringComparison.Ordinal);
    }
}
