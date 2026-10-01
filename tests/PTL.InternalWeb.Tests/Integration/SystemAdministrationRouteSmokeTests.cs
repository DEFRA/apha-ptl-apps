using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the System Administration Razor views (SystemAdministration,
// AdministrationCharge, WeightedPricingPlan, PostagePricingPlan) actually render at least once
// through the real MVC pipeline, rather than only being exercised via SystemAdministrationControllerTests.
public partial class SystemAdministrationRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private readonly WebApplicationFactory<Program> _factory;

    public SystemAdministrationRouteSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAdministrationChargeApiClient>();
                services.AddSingleton<IAdministrationChargeApiClient>(new FakeAdministrationChargeApiClient());
                services.RemoveAll<IWeightedPricingPlanApiClient>();
                services.AddSingleton<IWeightedPricingPlanApiClient>(new FakeWeightedPricingPlanApiClient());
                services.RemoveAll<IPostagePricingPlanApiClient>();
                services.AddSingleton<IPostagePricingPlanApiClient>(new FakePostagePricingPlanApiClient());
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
            }));
    }

    [Fact]
    public async Task SystemAdministration_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/SystemAdministration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemAdministration_RendersHeading()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/SystemAdministration");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-heading", body, StringComparison.OrdinalIgnoreCase);
    }

    // Locks in the _Layout.cshtml fix: ViewData["Title"] used to be ignored entirely, so every
    // page in the app rendered the exact same hard-coded <title>.
    [Fact]
    public async Task SystemAdministration_RendersPageTitleFromViewData()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/SystemAdministration");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>System Administration - GOV.UK</title>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministrationCharge_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/AdministrationCharge");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Locks in the AdministrationCharge.cshtml fix: the error-summary link's href must match a
    // real element id (previously it used the raw bracketed ModelState key, which never matched
    // asp-for's sanitised id, so the link silently went nowhere).
    [Fact]
    public async Task AdministrationCharge_Post_NegativePrice_RendersErrorSummaryWithMatchingAnchor()
    {
        var chargeId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAdministrationChargeApiClient>();
                services.AddSingleton<IAdministrationChargeApiClient>(new FakeAdministrationChargeApiClient
                {
                    Charges = [new AdministrationChargeResponse(chargeId, "Administration Charge", [new AdministrationChargeCurrencyPriceResponse(currencyId, 25.00m)])]
                });
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Currencies = [new CurrencyResponse(currencyId, "British Pound", "£", "£ - British Pound")]
                });
            }));
        var client = factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SystemAdministration/AdministrationCharge");

        var request = new HttpRequestMessage(HttpMethod.Post, "/SystemAdministration/AdministrationCharge")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Charges[0].AdministrationChargeId"] = chargeId.ToString(),
                ["Charges[0].Name"] = "Administration Charge",
                ["Charges[0].Prices[0].CurrencyId"] = currencyId.ToString(),
                ["Charges[0].Prices[0].CurrencyLabel"] = "£ - British Pound",
                ["Charges[0].Prices[0].Price"] = "-1.00"
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", body, StringComparison.Ordinal);
        Assert.Contains("id=\"charge-0-price-0\"", body, StringComparison.Ordinal);
        Assert.Contains("href=\"#charge-0-price-0\"", body, StringComparison.Ordinal);
        Assert.Contains("for=\"charge-0-price-0\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WeightedPricingPlan_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/WeightedPricingPlan");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostagePricingPlan_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/PostagePricingPlan");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The empty-state test above never exercises the @foreach row loop or the edit-mode cells
    // (form="postagePriceEditForm" cross-element binding, govuk-error-summary) since HasNoYears
    // short-circuits the view - this is the only test that renders that markup for real. Seeds a
    // second, non-editing row too, so both the read-only "Edit" link and the editing row's
    // "Save"/"Cancel" render in the same page - locks in that all three are styled as buttons
    // (govuk-button) rather than mixing plain govuk-link text with styled buttons.
    [Fact]
    public async Task PostagePricingPlan_WithEditingRow_RendersTableAndEditForm()
    {
        var postageId = Guid.NewGuid();
        var otherPostageId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPostagePricingPlanApiClient>();
                services.AddSingleton<IPostagePricingPlanApiClient>(new FakePostagePricingPlanApiClient
                {
                    Years = new PostagePricingPlanYearsResponse([new YearResponse(2026, "2026/27")], false, null, null)
                });
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Years = [new YearResponse(2026, "2026/27")],
                    PostagePricingPlans =
                    [
                        new PostagePricingPlanResponse(postageId, "Dryice", 5m, 10m, 15m, 2026),
                        new PostagePricingPlanResponse(otherPostageId, "Courier", 2m, 4m, 6m, 2026)
                    ]
                });
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/SystemAdministration/PostagePricingPlan?yearId=2026&editId={postageId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dry Ice", body, StringComparison.Ordinal);
        Assert.Contains("postagePriceEditForm", body, StringComparison.Ordinal);

        // Save: primary button (no secondary modifier). Cancel and the other row's Edit link are
        // still <a> elements (they navigate, not submit) but must be button-styled, not
        // plain govuk-link text, so all three read as one consistent button group. Checked
        // precisely (not just "govuk-link is absent anywhere") since the shared layout's footer/
        // phase-banner render their own unrelated govuk-link elements on every page.
        Assert.Contains("type=\"submit\">Save</button>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-button--secondary govuk-!-margin-bottom-0\" data-module=\"govuk-button\" type=\"submit\">Save", body, StringComparison.Ordinal);
        Assert.Contains("govuk-button--secondary govuk-!-margin-bottom-0\" data-module=\"govuk-button\" href=", body, StringComparison.Ordinal);
        Assert.Contains(">Edit</a>", body, StringComparison.Ordinal);
        Assert.Contains(">Cancel</a>", body, StringComparison.Ordinal);
    }

    // Locks in the Postage equivalent of the AdministrationCharge anchor fix - here the ModelState
    // keys are already flat ("UKPrice" etc.) so the fix was adding a matching id/label, not
    // reshaping the error list.
    [Fact]
    public async Task PostagePricingPlanSave_Post_NegativePrice_RendersErrorSummaryWithMatchingAnchor()
    {
        var postageId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPostagePricingPlanApiClient>();
                services.AddSingleton<IPostagePricingPlanApiClient>(new FakePostagePricingPlanApiClient
                {
                    Years = new PostagePricingPlanYearsResponse([new YearResponse(2026, "2026/27")], false, null, null)
                });
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Years = [new YearResponse(2026, "2026/27")],
                    PostagePricingPlans = [new PostagePricingPlanResponse(postageId, "Courier", 5m, 10m, 15m, 2026)]
                });
            }));
        var client = factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, $"/SystemAdministration/PostagePricingPlan?yearId=2026&editId={postageId}");

        var request = new HttpRequestMessage(HttpMethod.Post, "/SystemAdministration/PostagePricingPlanSave")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["PostageId"] = postageId.ToString(),
                ["YearId"] = "2026",
                ["UKPrice"] = "-1.00",
                ["EUPrice"] = "10.00",
                ["NonEUPrice"] = "15.00"
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", body, StringComparison.Ordinal);
        Assert.Contains("id=\"UKPrice\"", body, StringComparison.Ordinal);
        Assert.Contains("href=\"#UKPrice\"", body, StringComparison.Ordinal);
        Assert.Contains("for=\"UKPrice\"", body, StringComparison.Ordinal);
    }

    private static async Task<(string Token, string Cookie)> GetAntiforgeryAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        var token = AntiforgeryTokenPattern().Match(body).Groups[1].Value;
        var cookie = string.Join("; ", response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(c => c.Split(';')[0])
            : []);
        return (token, cookie);
    }
}

