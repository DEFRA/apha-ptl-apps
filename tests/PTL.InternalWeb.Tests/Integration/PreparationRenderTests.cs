using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/Preparation.cshtml through the real view engine - legacy
// Distribution/PreparationSheet.aspx parity (unsaved-changes protection, uninitialised month).
public class PreparationRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Url = "/Distribution/Preparation?year=2026&monthId=5";

    private static readonly Guid SchemeId = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public PreparationRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private static PreparationSchemeResponse Scheme(bool withSamples = true) =>
        new(
            SchemeId, "PT0060", "Mastitis bovine", "17152A/BA", 9, "Keep chilled",
            DiaryEntry: null, PrepStartDate: null, PrepCompleteDate: null,
            Samples: withSamples ? [new PreparationSampleResponse(Guid.NewGuid(), "S1", "Internal 1")] : []);

    private async Task<string> GetHtmlAsync()
    {
        _apiClient.Preparation = new PreparationSheetResponse(Guid.NewGuid(), 2026, 5, [Scheme()]);
        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Input(string html, string id) =>
        Regex.Match(html, $"<input[^>]*id=\"{Regex.Escape(id)}\"[^>]*>").Value;

    [Fact]
    public async Task Get_RendersTheSharedUnsavedChangesBannerHidden()
    {
        var html = await GetHtmlAsync();

        var banner = Regex.Match(html, "<section[^>]*id=\"unsaved-changes\"[^>]*>").Value;
        Assert.Contains("govuk-notification-banner", banner, StringComparison.Ordinal);
        Assert.Contains("hidden", banner, StringComparison.Ordinal);
        Assert.Contains("You have changes that have not been saved.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_LoadsTheSharedUnsavedChangesScript()
    {
        var html = await GetHtmlAsync();

        Assert.Contains("/js/unsaved-changes.js", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_UsesNoBrowserDialogs()
    {
        var html = await GetHtmlAsync();

        Assert.DoesNotContain("confirm(", html, StringComparison.Ordinal);
        Assert.DoesNotContain("alert(", html, StringComparison.Ordinal);
        Assert.DoesNotContain("beforeunload", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_WatchesTheFormButOnlyStageTickBoxesCountAsChanges()
    {
        var html = await GetHtmlAsync();

        Assert.Matches("<form[^>]*data-unsaved-watch", html);
        Assert.Contains("data-unsaved-ignore", Input(html, "preparation-print-select-all"), StringComparison.Ordinal);
        Assert.Contains("data-unsaved-ignore", Input(html, "Schemes_0__Selected"), StringComparison.Ordinal);
        Assert.DoesNotContain("data-unsaved-ignore", Input(html, "Schemes_0__DiaryEntryChecked"), StringComparison.Ordinal);
        Assert.DoesNotContain("data-unsaved-ignore", Input(html, "Schemes_0__PrepStartChecked"), StringComparison.Ordinal);
        Assert.DoesNotContain("data-unsaved-ignore", Input(html, "Schemes_0__PrepCompleteChecked"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_SaveApplyAndCancelStaySubmitButtons_SoTheyAreNeverIntercepted()
    {
        var html = await GetHtmlAsync();

        foreach (var command in new[] { "save", "apply", "cancel" })
        {
            Assert.Matches($"<button type=\"submit\" name=\"command\" value=\"{command}\"", html);
        }
    }

    [Fact]
    public async Task Get_SchemeWithoutSamples_ShowsNoStageTickBoxes()
    {
        _apiClient.Preparation = new PreparationSheetResponse(Guid.NewGuid(), 2026, 5, [Scheme(withSamples: false)]);

        var response = await _factory.CreateClient().GetAsync(Url);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("The sample numbers for this scheme have not yet been defined", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Schemes_0__DiaryEntryChecked", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2026, 5, 2026)]
    [InlineData(2026, 2, 2025)]
    public async Task Get_MonthNotInitialised_RedirectsToTheDashboardForTheFinancialYear(int year, int monthId, int financialYear)
    {
        _apiClient.Preparation = null;
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Distribution/Preparation?year={year}&monthId={monthId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.Contains($"year={financialYear}", location, StringComparison.Ordinal);
        Assert.DoesNotContain("Preparation", location, StringComparison.Ordinal);
    }
}
