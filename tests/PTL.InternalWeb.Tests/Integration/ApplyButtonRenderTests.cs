using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.ApiClient.Distribution;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Country;
using PTL.Contracts.Distribution;
using PTL.Contracts.Lookup;
using PTL.Contracts.User;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Apply Button - Legacy Parity. Legacy PT-LIMS offers Save (persist and leave), Apply (persist and stay)
// and Cancel (leave without saving) on the Distribution pages below, and only on those: the pages legacy
// leaves with Save and Cancel alone must still show no Apply.
public class ApplyButtonRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid Scheme = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid Participant = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid Sample = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid Test = Guid.Parse("dddddddd-0000-0000-0000-000000000003");

    private static readonly Guid Currency = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid CountryType = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public ApplyButtonRenderTests(WebApplicationFactory<Program> factory)
    {
        // The System Administration pages below render a form only when they have rows, so each fake is seeded
        // with the one row its page needs.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Currencies = [new CurrencyResponse(Currency, "British Pound", "£", "£ - British Pound")]
                });
                services.RemoveAll<IAdministrationChargeApiClient>();
                services.AddSingleton<IAdministrationChargeApiClient>(new FakeAdministrationChargeApiClient
                {
                    Charges = [new AdministrationChargeResponse(Guid.NewGuid(), "Administration Charge", [new AdministrationChargeCurrencyPriceResponse(Currency, 25.00m)])]
                });
                services.RemoveAll<ICountryApiClient>();
                services.AddSingleton<ICountryApiClient>(new FakeCountryApiClient
                {
                    Countries = [new PTL.Contracts.Country.CountryResponse(Guid.NewGuid(), "England", CountryType, "UK", 0)],
                    CountryTypes = [new CountryTypeResponse(CountryType, "UK")]
                });
                services.RemoveAll<IUserApiClient>();
                services.AddSingleton<IUserApiClient>(new FakeUserApiClient
                {
                    TestConsultants = [new UserResponse(Guid.NewGuid(), "m100001", "Jane Smith", "Jane", "Smith", "jane@apha.gov.uk", "Science", false, null)]
                });
            }));
    }

    private async Task<string> MainAsync(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ButtonGroupHtml.MainContent(await response.Content.ReadAsStringAsync());
    }

    // ---- The twelve pages legacy gives an Apply button ----

    [Fact]
    public async Task Schedule_ShowsApplyBetweenSaveAndCancel()
    {
        _apiClient.Schedule = new MonthlyDistributionResponse(Guid.NewGuid(), 2026, 5, []);

        ButtonGroupHtml.AssertApplyBetweenSaveAndCancel(await MainAsync("/Distribution/Schedule?year=2026&monthId=5"));
    }

    [Fact]
    public async Task Preparation_ShowsApplyBetweenSaveAndCancel()
    {
        _apiClient.Preparation = new PreparationSheetResponse(Guid.NewGuid(), 2026, 5, []);

        ButtonGroupHtml.AssertApplyBetweenSaveAndCancel(await MainAsync("/Distribution/Preparation?year=2026&monthId=5"));
    }

    [Fact]
    public async Task ResultsReceipt_ShowsApplyBetweenSaveAndCancel()
    {
        _apiClient.ResultsReceipt = new ResultsReceiptResponse(Guid.NewGuid(), 2026, 5, []);

        ButtonGroupHtml.AssertApplyBetweenSaveAndCancel(await MainAsync("/Distribution/ResultsReceipt?year=2026&monthId=5"));
    }

    [Fact]
    public async Task SampleReceipt_ShowsApplyBetweenSaveAndCancel()
    {
        // The page is a form only when it is editable and has samples; with neither it renders a Back link alone.
        _apiClient.SampleReceipt = new SampleReceiptResponse(
            "D1 : BT Brucella", "Labcode L001: Lab One", IsReadOnly: false,
            [new SampleReceiptTestResponse(Test, "BT", [new SampleReceiptSampleResponse(Sample, "S1", true, false, false)])]);

        ButtonGroupHtml.AssertApplyBetweenSaveAndCancel(
            await MainAsync($"/Distribution/SampleReceipt?year=2026&monthId=5&schemeId={Scheme}&participantId={Participant}"));
    }

    [Fact]
    public async Task Tabulations_ShowsApplyBetweenSaveAndCancel()
    {
        _apiClient.Tabulations = new TabulationsResponse(2026, 5, []);

        ButtonGroupHtml.AssertApplyBetweenSaveAndCancel(await MainAsync("/Distribution/Tabulations?year=2026&monthId=5"));
    }

    [Fact]
    public async Task Packaging_ShowsApplyBetweenSaveAndCancel()
    {
        ButtonGroupHtml.AssertApplyBetweenSaveAndCancel(await MainAsync("/Distribution/Packaging?year=2026&monthId=5"));
    }

    [Fact]
    public async Task ActualResults_ShowsApplyBetweenSaveAndCancel()
    {
        _apiClient.ActualResults = new ActualResultsResponse(
            Participant, Scheme, "D1 S1: Brucella", "Audit", new DateTime(2030, 1, 1), false, []);

        ButtonGroupHtml.AssertApplyBetweenSaveAndCancel(
            await MainAsync($"/Distribution/ActualResults?year=2026&monthId=5&schemeId={Scheme}&participantId={Participant}"));
    }

    [Fact]
    public async Task AmendResults_ShowsApplyBetweenSaveAndCancel()
    {
        _apiClient.AmendResults = new AmendResultsResponse(Scheme, "D1a/S1 BT: Brucella", false, false, [], [], [], [], []);

        ButtonGroupHtml.AssertApplyBetweenSaveAndCancel(await MainAsync($"/Distribution/AmendResults?year=2026&monthId=5&schemeId={Scheme}"));
    }

    // ---- The pages legacy leaves with Save and Cancel alone ----

    [Fact]
    public async Task IntendedResults_StillHasNoApply()
    {
        _apiClient.IntendedResults = new IntendedResultsResponse(Scheme, "D1a", "S1", "Brucella", RequiresAuditReason: false, []);

        ButtonGroupHtml.AssertSaveAndCancelWithoutApply(await MainAsync($"/Distribution/IntendedResults?year=2026&monthId=5&schemeId={Scheme}"));
    }

    [Fact]
    public async Task SampleNumbers_StillHasNoApply()
    {
        _apiClient.SampleNumbers = new SampleNumbersResponse(
            Scheme, "17152A/BA", "PT0060", "Mastitis bovine", 9, "Server comments", false, false,
            [new SampleNumberRowResponse(Sample, "S1", "Internal 1", "External 1", "Tabulation 1", "TC 1", [new SampleAllocationResponse(Guid.NewGuid(), Test, true)])],
            [new SampleNumbersTestResponse(Test, "Test 1", ["Result 1"], ["Method 1"], ["Category 1"])]);

        ButtonGroupHtml.AssertSaveAndCancelWithoutApply(await MainAsync($"/Distribution/SampleNumbers?year=2026&monthId=5&schemeId={Scheme}"));
    }

    [Fact]
    public async Task ViewComments_StillHasNoApply()
    {
        _apiClient.TestConsultantComments = new TestConsultantCommentsResponse(
            Scheme, "D1a/S1 BT: Brucella", "Comments by Dr A", "<p>Stored</p>", true, [], [".pdf"], "PDF", "10MB");

        ButtonGroupHtml.AssertSaveAndCancelWithoutApply(await MainAsync($"/Distribution/ViewComments?year=2026&monthId=5&schemeId={Scheme}"));
    }

    [Fact]
    public async Task Participants_StillHasNoApply()
    {
        _apiClient.Participants = new DistributionParticipantsResponse(
            Scheme, 2026, 5, "S1", "Brucella", "D1a/S1", false,
            [new DistributionParticipantResponse(Participant, "L001: Lab One", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), string.Empty, false)],
            []);

        Assert.Empty(ButtonGroupHtml.Applies(await MainAsync($"/Distribution/Participants?year=2026&monthId=5&schemeId={Scheme}")));
    }

    // ---- System Administration pages legacy leaves with Save and Cancel alone ----
    // Their siblings ViewerManagement and ExternalTestConsultantManagement do have Apply in legacy
    // (Admin/ManageViewers.aspx, Admin/ManageExternalTCs.aspx), so these three are the ones at risk
    // of gaining one by association.

    [Theory]
    // legacy Admin/CountryManagement.aspx
    [InlineData("CountryManagement")]
    // legacy Admin/TCDepartment.aspx
    [InlineData("InternalTestConsultantDepartment")]
    // legacy Admin/AdministrationChargeCurrency.aspx
    [InlineData("AdministrationCharge")]
    public async Task SystemAdministrationPage_StillHasNoApply(string action)
    {
        var main = await MainAsync($"/SystemAdministration/{action}");

        // Guards against passing because the page rendered no buttons at all.
        Assert.Contains(ButtonGroupHtml.Groups(main).SelectMany(g => g), b => b.Label == "Save");
        Assert.Empty(ButtonGroupHtml.Applies(main));
    }
}

// Shared reading of a GOV.UK button group: these groups hold only buttons and links, so start tags are
// tokenised with a regex (as BreadcrumbHtml and SchemeFormPage do) rather than pulling in an HTML parser.
internal static partial class ButtonGroupHtml
{
    [GeneratedRegex("<main[\\s\\S]*?</main>")]
    private static partial Regex MainPattern();

    // Some groups carry extra classes ("govuk-button-group noprint", "... govuk-!-margin-top-2"), so match any
    // class list that starts with govuk-button-group. The groups these tests read hold buttons and links only,
    // so the lazy </div> is the group's own closing tag.
    [GeneratedRegex("<div class=\"govuk-button-group[^\"]*\">(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex GroupPattern();

    [GeneratedRegex("<(button|a)\\b([^>]*)>(.*?)</\\1>", RegexOptions.Singleline)]
    private static partial Regex ButtonPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    internal sealed record Button(string Element, string Attributes, string Label);

    // Not named Main: the test project builds as an Exe, so a static Main(string) is taken for an entry point.
    public static string MainContent(string html)
    {
        var main = MainPattern().Match(html).Value;
        Assert.True(main.Length > 0, "The page has no <main> content.");
        return main;
    }

    private static Button Read(Match match) =>
        new(match.Groups[1].Value, match.Groups[2].Value, WebUtility.HtmlDecode(TagPattern().Replace(match.Groups[3].Value, string.Empty)).Trim());

    public static IReadOnlyList<IReadOnlyList<Button>> Groups(string main) =>
        [.. GroupPattern().Matches(main).Select(g => (IReadOnlyList<Button>)[.. ButtonPattern().Matches(g.Groups[1].Value).Select(Read)])];

    public static IReadOnlyList<Button> Applies(string main) =>
        [.. Groups(main).SelectMany(g => g).Where(b => b.Label == "Apply")];

    private static IReadOnlyList<Button> SaveGroup(string main) =>
        Assert.Single(Groups(main), g => g.Count > 0 && g[0].Label == "Save");

    // One Apply submit button in the page's Save/Cancel group, sitting between them, with Save and Cancel untouched.
    public static void AssertApplyBetweenSaveAndCancel(string main, string? formAction = null)
    {
        var group = SaveGroup(main);

        Assert.Equal(["Save", "Apply", "Cancel"], group.Take(3).Select(b => b.Label));
        Assert.Equal("button", group[1].Element);
        Assert.Contains("type=\"submit\"", group[1].Attributes, StringComparison.Ordinal);
        Assert.Contains("govuk-button--secondary", group[1].Attributes, StringComparison.Ordinal);
        if (formAction is null)
        {
            Assert.Contains("name=\"command\" value=\"apply\"", group[1].Attributes, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains($"formaction=\"{formAction}\"", group[1].Attributes, StringComparison.Ordinal);
        }

        // Save and Cancel keep their own markup and stay single.
        Assert.Single(Applies(main));
        Assert.Single(Groups(main).SelectMany(g => g), b => b.Label == "Save");
        Assert.Single(Groups(main).SelectMany(g => g), b => b.Label == "Cancel");
    }

    // The page keeps its Save/Cancel group - whatever else legacy puts in it - and gains no Apply.
    public static void AssertSaveAndCancelWithoutApply(string main)
    {
        var group = SaveGroup(main);

        Assert.Equal("Save", group[0].Label);
        Assert.Contains(group, b => b.Label == "Cancel");
        Assert.Empty(Applies(main));
    }
}
