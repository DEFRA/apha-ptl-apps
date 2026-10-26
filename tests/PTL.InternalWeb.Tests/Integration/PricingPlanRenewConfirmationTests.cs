using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;
using PTL.Contracts.WeightedPricingPlan;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renew pricing plan for financial year (Postage and Weighted): the browser confirm() is replaced by the shared
// confirmation page, and a successful renewal returns to the plan with the shared success banner.
public partial class PricingPlanRenewConfirmationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakePostagePricingPlanApiClient _postage;
    private readonly FakeWeightedPricingPlanApiClient _weighted;

    public PricingPlanRenewConfirmationTests(WebApplicationFactory<Program> factory)
    {
        YearResponse[] years = [new(2026, "2026/27")];
        _postage = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse(years, true, 2027, "2027/28"),
            RenewResponse = new PostagePricingPlanRenewResponse(true, "The postage pricing plan has successfully been renewed for the financial year 2027/28.")
        };
        _weighted = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse(years, true, 2027, "2027/28"),
            RenewResponse = new WeightedPricingPlanRenewResponse(true, "The weighted pricing plan has successfully been renewed for the financial year 2027/28.")
        };

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPostagePricingPlanApiClient>();
                services.AddSingleton<IPostagePricingPlanApiClient>(_postage);
                services.RemoveAll<IWeightedPricingPlanApiClient>();
                services.AddSingleton<IWeightedPricingPlanApiClient>(_weighted);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient { Years = years });
            }));
    }

    private int RenewCalls(string page) => page == "PostagePricingPlan" ? _postage.RenewCallCount : _weighted.RenewCallCount;

    private static string RenewAction(string page) => page == "PostagePricingPlan" ? "PostagePricingPlanRenew" : "Renew";

    private async Task<(HttpResponseMessage Response, string Cookies)> PostRenewAsync(string page, string? confirmation)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var form = await client.GetAsync($"/SystemAdministration/{page}?yearId=2026");
        var token = AntiforgeryTokenPattern().Match(await form.Content.ReadAsStringAsync()).Groups[1].Value;
        var cookie = string.Join("; ", form.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.Select(c => c.Split(';')[0]) : []);

        var fields = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token), new("yearId", "2026") };
        if (confirmation is not null)
        {
            fields.Add(new("confirmation", confirmation));
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/SystemAdministration/{RenewAction(page)}") { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add("Cookie", cookie);
        return (await client.SendAsync(request), cookie);
    }

    [Theory]
    [InlineData("PostagePricingPlan")]
    [InlineData("WeightedPricingPlan")]
    public async Task Page_RenewIsAPlainButtonWithNoBrowserDialog(string page)
    {
        var html = await _factory.CreateClient().GetStringAsync($"/SystemAdministration/{page}?yearId=2026");

        Assert.Matches("<button class=\"govuk-button\" data-module=\"govuk-button\" type=\"submit\">\\s*Renew pricing plan for financial year 2027/28\\s*</button>", html);
        Assert.DoesNotContain("confirm(", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PostagePricingPlan")]
    [InlineData("WeightedPricingPlan")]
    public async Task Renew_ShowsTheConfirmationPageWithConfirmAndCancelButtonsAndDoesNotRenew(string page)
    {
        var (response, _) = await PostRenewAsync(page, confirmation: null);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Renew pricing plan for financial year 2027/28</h1>", html, StringComparison.Ordinal);
        Assert.Contains("for the financial year 2027/28", html, StringComparison.Ordinal);
        Assert.Contains("govuk-warning-text", html, StringComparison.Ordinal);
        Assert.Matches("<input type=\"hidden\" name=\"yearId\" value=\"2026\" />", html);
        Assert.Matches("<button type=\"submit\" class=\"govuk-button\" data-module=\"govuk-button\">Renew pricing plan</button>", html);
        Assert.Matches("<button type=\"submit\" form=\"confirmation-cancel\" class=\"govuk-button govuk-button--secondary\"[^>]*>Cancel</button>", html);
        Assert.DoesNotContain("confirm(", html, StringComparison.Ordinal);
        Assert.Equal(0, RenewCalls(page));
    }

    [Theory]
    [InlineData("PostagePricingPlan")]
    [InlineData("WeightedPricingPlan")]
    public async Task Cancel_ReturnsToThePlanForTheSameYearAndChangesNothing(string page)
    {
        var (response, _) = await PostRenewAsync(page, confirmation: "cancel");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/SystemAdministration/{page}?yearId=2026", response.Headers.Location!.OriginalString);
        Assert.Equal(0, RenewCalls(page));
    }

    [Theory]
    [InlineData("PostagePricingPlan")]
    [InlineData("WeightedPricingPlan")]
    public async Task Confirm_RenewsThenGoesToTheSystemAdministrationIndexWithASuccessBanner(string page)
    {
        var (response, cookie) = await PostRenewAsync(page, confirmation: "confirm");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/SystemAdministration/Index", response.Headers.Location!.OriginalString);
        Assert.Equal(1, RenewCalls(page));

        var next = new HttpRequestMessage(HttpMethod.Get, response.Headers.Location);
        var tempDataCookies = response.Headers.TryGetValues("Set-Cookie", out var set) ? set.Select(c => c.Split(';')[0]) : [];
        next.Headers.Add("Cookie", string.Join("; ", tempDataCookies.Prepend(cookie)));
        var html = await (await _factory.CreateClient().SendAsync(next)).Content.ReadAsStringAsync();

        Assert.Contains("govuk-notification-banner--success", html, StringComparison.Ordinal);
        Assert.Contains("successfully been renewed for the financial year 2027/28.", html, StringComparison.Ordinal);
    }
}
