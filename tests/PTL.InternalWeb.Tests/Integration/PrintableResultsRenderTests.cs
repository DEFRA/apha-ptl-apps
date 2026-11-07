using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/PrintableResults.cshtml through the real view engine - legacy
// Internal User/PrintableResults.aspx parity (results-entry address from External:AppUrl, sample
// instructions, footer, scheme resolved by id).
public class PrintableResultsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid SchemeId = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly string Url = $"/Distribution/PrintableResults?year=2026&monthId=5&schemeId={SchemeId}";

    private const string LegacyUatAddress = "new.uat-science.apha.gov.uk";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public PrintableResultsRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private static IntendedResultsSampleRowResponse Sample(string number, string externalDescription = "") =>
        new(Guid.NewGuid(), number, "Internal " + number, true, [], externalDescription);

    private static IntendedResultsResponse Page(
        IReadOnlyList<IntendedResultsSampleRowResponse>? samples = null,
        bool dateOfReceipt = false,
        bool isAccredited = false,
        string externalAppUrl = "https://ptl.example.test/ProficiencyTesting") =>
        new(
            SchemeId, "17152A/BA", "PT0060", "Mastitis bovine", false,
            [new IntendedResultsTestResponse(Guid.NewGuid(), "Test A", [], [], samples ?? [Sample("S1")])],
            DateOfReceipt: dateOfReceipt,
            DistributionDate: new DateTime(2026, 4, 14),
            DeadlineDate: new DateTime(2026, 5, 1),
            IsAccredited: isAccredited,
            ExternalAppUrl: externalAppUrl);

    private async Task<string> GetHtmlAsync(IntendedResultsResponse? page = null)
    {
        _apiClient.IntendedResults = page ?? Page();

        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Text(string fragment) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim());

    // ---- Item 1: results-entry address comes from the API's External:AppUrl ----

    [Fact]
    public async Task Get_ExternalAppUrlFromApi_IsPrintedAsTheResultsEntryAddress()
    {
        var html = await GetHtmlAsync();

        Assert.Contains("Enter results online at:", Text(html), StringComparison.Ordinal);
        Assert.Contains("<strong>https://ptl.example.test/ProficiencyTesting</strong>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_DifferentEnvironment_PrintsThatEnvironmentsAddress()
    {
        var html = await GetHtmlAsync(Page(externalAppUrl: "https://ptl.prod.example.test/"));

        Assert.Contains("<strong>https://ptl.prod.example.test/</strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ptl.example.test/ProficiencyTesting", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_NeverContainsTheOldHardCodedUatAddress()
    {
        Assert.DoesNotContain(LegacyUatAddress, await GetHtmlAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(LegacyUatAddress, await GetHtmlAsync(Page(externalAppUrl: "")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_ExternalAppUrlNotConfigured_OmitsTheResultsEntryLineButKeepsTheAddress()
    {
        var html = await GetHtmlAsync(Page(externalAppUrl: ""));

        Assert.DoesNotContain("Enter results online at:", html, StringComparison.Ordinal);
        Assert.Contains("Animal and Plant Health Agency", html, StringComparison.Ordinal);
    }
    // ---- Item 4: empty sample instructions ----

    [Fact]
    public async Task Get_SampleWithEmptyInstruction_RowIsHidden()
    {
        var html = await GetHtmlAsync(Page([Sample("S1"), Sample("S2", "Keep cool"), Sample("S3")]));

        var table = Regex.Match(html, "<table class=\"tblSampleInstruction\">.*?</table>", RegexOptions.Singleline).Value;
        Assert.Contains("Keep cool", table, StringComparison.Ordinal);
        Assert.Contains(">S2<", table, StringComparison.Ordinal);
        Assert.DoesNotContain(">S1<", table, StringComparison.Ordinal);
        Assert.DoesNotContain(">S3<", table, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_NoSampleHasAnInstruction_WholeSampleInstructionsSectionIsHidden()
    {
        var html = await GetHtmlAsync(Page([Sample("S1"), Sample("S2")]));

        Assert.DoesNotContain("tblSampleInstruction", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sample Instruction", html, StringComparison.Ordinal);
    }

    // ---- Item 6: the extra Date of Receipt / Date of Testing footer block is gone ----

    [Fact]
    public async Task Get_DoesNotRenderTheUnconditionalDateOfReceiptAndDateOfTestingBlock()
    {
        var text = Text(await GetHtmlAsync());

        Assert.DoesNotContain("Date of Testing", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Date of Receipt:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_SchemeRecordsDateOfReceipt_KeepsTheGenuineDistributionItemsLine()
    {
        var text = Text(await GetHtmlAsync(Page(dateOfReceipt: true)));

        Assert.Contains("Date Of Receipt: _____________", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Date of Testing", text, StringComparison.Ordinal);
    }

    // ---- Item 5: scheme resolved by id ----

    [Fact]
    public async Task Get_SchemeResolved_ShowsItsDistributionDateDeadlineAndUkasMark()
    {
        var html = await GetHtmlAsync(Page(isAccredited: true));

        var text = Text(html);
        Assert.Contains("DATE OF DISTRIBUTION: 14/04/2026", text, StringComparison.Ordinal);
        Assert.Contains("no later than 23:00 GMT on 01/05/2026", text, StringComparison.Ordinal);
        Assert.Contains("ukas.png", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_SchemeNotAccredited_ShowsNoUkasMark()
    {
        Assert.DoesNotContain("ukas.png", await GetHtmlAsync(Page(isAccredited: false)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_SchemeCannotBeResolved_ReturnsNotFound()
    {
        _apiClient.IntendedResults = null;

        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
