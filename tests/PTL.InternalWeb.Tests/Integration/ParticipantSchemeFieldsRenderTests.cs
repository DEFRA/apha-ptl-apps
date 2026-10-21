using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Lookup;
using PTL.Contracts.Participant;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.Features.ParticipantScheme;
using PTL.InternalWeb.Tests.Features.ParticipantScheme;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/ParticipantScheme/Views/Edit.cshtml and Create.cshtml (the shared
// _ParticipantSchemeFields partial) against legacy ParticipantScheme.aspx.
public class ParticipantSchemeFieldsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid ParticipantSchemeId = Guid.Parse("ffffffff-0000-0000-0000-0000000000b1");
    private static readonly Guid ContractId = Guid.Parse("ffffffff-0000-0000-0000-0000000000b2");
    private static readonly Guid CustomerId = Guid.Parse("ffffffff-0000-0000-0000-0000000000b3");
    private static readonly Guid SchemeId = Guid.Parse("ffffffff-0000-0000-0000-0000000000b4");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly WebApplicationFactory<Program> _overrideUserFactory;

    public ParticipantSchemeFieldsRenderTests(WebApplicationFactory<Program> factory)
    {
        var yearId = DateTime.UtcNow.Year;
        var participantSchemes = new FakeParticipantSchemeApiClient
        {
            ParticipantSchemeResponse = ParticipantSchemeControllerTests.SampleParticipantScheme(ParticipantSchemeId, ContractId) with
            {
                SchemeId = SchemeId,
                // February is already posted and not selected; January is the item's only month.
                CanEditFeb = false,
            },
            Context = new ParticipantSchemeContextResponse([true, false, true, true, true, true, true, true, true, true, true, true], [1]),
        };
        var contracts = new FakeContractApiClient
        {
            ContractResponse = ParticipantSchemeControllerTests.SampleContract(ContractId, CustomerId) with { YearId = yearId },
        };
        var schemes = new FakeSchemeApiClient
        {
            SchemeResponse = ParticipantSchemeControllerTests.SampleScheme(SchemeId, yearId, jan: true, feb: true),
        };

        void ConfigureFakes(IServiceCollection services)
        {
            services.RemoveAll<IParticipantSchemeApiClient>();
            services.AddSingleton<IParticipantSchemeApiClient>(participantSchemes);
            services.RemoveAll<IContractApiClient>();
            services.AddSingleton<IContractApiClient>(contracts);
            services.RemoveAll<IParticipantApiClient>();
            services.AddSingleton<IParticipantApiClient>(new FakeParticipantApiClient());
            services.RemoveAll<ISchemeApiClient>();
            services.AddSingleton<ISchemeApiClient>(schemes);
            services.RemoveAll<ILookupApiClient>();
            services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient { WeightedPricingYears = [new YearResponse(yearId, $"{yearId}/{yearId + 1}")] });
        }

        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(ConfigureFakes));

        _overrideUserFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ConfigureFakes(services);
                services.Configure<TestAuthHandlerOptions>(
                    TestAuthHandler.SchemeName,
                    options => options.ResolvedRoles = "Contracts Admin,Override Schemes");
            }));
    }

    private async Task<string> MainContentAsync(string url, bool asOverrideUser = false)
    {
        var response = await (asOverrideUser ? _overrideUserFactory : _factory).CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        return Regex.Match(html, "<main.*?</main>", RegexOptions.Singleline).Value;
    }

    private static string InputTag(string html, string id) => Regex.Match(html, $"<input[^>]*id=\"{id}\"[^>]*>").Value;

    [Fact]
    public async Task Edit_SetsOfSamplesRequired_IsADropDownOfOneToTen()
    {
        var html = await MainContentAsync($"/ParticipantScheme/Edit/{ParticipantSchemeId}");

        var select = Regex.Match(html, "<select[^>]*id=\"NumberOfSetsRequired\".*?</select>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(select);
        var values = Regex.Matches(select, "<option[^>]*value=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(Enumerable.Range(1, 10), values);
        Assert.Contains("Sets of Samples Required", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_DoesNotShowAPrice_BecauseLegacyOnlyShowsItOnContractItems()
    {
        var html = await MainContentAsync($"/ParticipantScheme/Edit/{ParticipantSchemeId}");

        Assert.DoesNotContain("Price:", html, StringComparison.Ordinal);
        Assert.DoesNotContain("42.50", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_MonthTable_UsesTheLegacyHeadingAndLocksPostedAndUnofferedMonths()
    {
        var html = await MainContentAsync($"/ParticipantScheme/Edit/{ParticipantSchemeId}");

        Assert.Contains("<h2 class=\"govuk-heading-m\">Months</h2>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", InputTag(html, "DistributionMonthJan"), StringComparison.Ordinal);
        Assert.Contains("disabled", InputTag(html, "DistributionMonthFeb"), StringComparison.Ordinal);
        Assert.Contains("disabled", InputTag(html, "DistributionMonthMar"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_Months_AreListedInLegacyAprilToMarchOrder()
    {
        var html = await MainContentAsync($"/ParticipantScheme/Edit/{ParticipantSchemeId}");

        var names = Regex.Matches(html, "<td class=\"govuk-table__cell\">([A-Z][a-z]+)</td>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["April", "May", "June", "July", "August", "September", "October", "November", "December", "January", "February", "March"], names);
    }

    [Fact]
    public async Task Edit_OverrideButton_IsHiddenFromUsersWithoutTheOverrideSchemesRole()
    {
        var html = await MainContentAsync($"/ParticipantScheme/Edit/{ParticipantSchemeId}");

        Assert.DoesNotContain("ptl-override-button", html.Replace("document.getElementById('ptl-override-button')", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("OverrideModeActiveField", html.Replace("document.getElementById('OverrideModeActiveField')", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_OverrideButton_IsShownToUsersInTheOverrideSchemesRole()
    {
        var html = await MainContentAsync($"/ParticipantScheme/Edit/{ParticipantSchemeId}", asOverrideUser: true);

        Assert.Matches("<button[^>]*id=\"ptl-override-button\"[^>]*>Override</button>", html);
        Assert.Matches("<input[^>]*id=\"OverrideModeActiveField\"", html);
    }

    [Fact]
    public async Task Edit_PricingPlan_CarriesTheSchemeCountsTheScriptNeedsAndOffersWeightedAndProRata()
    {
        var html = await MainContentAsync($"/ParticipantScheme/Edit/{ParticipantSchemeId}");

        var select = Regex.Match(html, "<select[^>]*id=\"PricingPlan\".*?</select>", RegexOptions.Singleline).Value;
        Assert.Contains("data-number-on-scheme=\"2\"", select, StringComparison.Ordinal);
        Assert.Contains("data-number-available=\"2\"", select, StringComparison.Ordinal);
        Assert.Contains("data-is-new=\"false\"", select, StringComparison.Ordinal);
        Assert.Contains("Weighted pricing plan", select, StringComparison.Ordinal);
        Assert.Contains("Pro rata", select, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", select[..select.IndexOf('>', StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_InitialisedMonth_ShowsAnInlineWarningInsteadOfABrowserAlert()
    {
        var html = await MainContentAsync($"/ParticipantScheme/Edit/{ParticipantSchemeId}");

        Assert.Matches("(?s)govuk-warning-text.*?This Distribution has already been initialised \\(January\\)", html);
        Assert.DoesNotContain("alert(", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_PackingInstructions_IsAMultiLineTextArea()
    {
        var html = await MainContentAsync($"/ParticipantScheme/Edit/{ParticipantSchemeId}");

        Assert.Matches("<textarea[^>]*id=\"PackingInstructions\"", html);
        Assert.Contains("Other Packaging Requirements", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_WithScheme_StartsWithEveryOpenMonthTickedAndFullSchemePricing()
    {
        var html = await MainContentAsync($"/ParticipantScheme/Create?contractId={ContractId}&customerId={CustomerId}&participantId={Guid.NewGuid()}&schemeId={SchemeId}");

        Assert.Contains("checked", InputTag(html, "DistributionMonthJan"), StringComparison.Ordinal);
        Assert.DoesNotContain("checked", InputTag(html, "DistributionMonthFeb"), StringComparison.Ordinal);
        Assert.Contains("disabled", InputTag(html, "DistributionMonthFeb"), StringComparison.Ordinal);
        var select = Regex.Match(html, "<select[^>]*id=\"PricingPlan\".*?</select>", RegexOptions.Singleline).Value;
        Assert.Contains("data-is-new=\"true\"", select, StringComparison.Ordinal);
        Assert.Contains("Full scheme pricing", select, StringComparison.Ordinal);
    }
}
