using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Full-pipeline smoke tests for Create New Order (Story 744642, legacy PendingContractOrder.aspx stage 1) - proves the
// Razor views render through real routing/authorization and that the AJAX body swap endpoint is antiforgery-protected.
public partial class OrderRouteSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public OrderRouteSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    private HttpClient CreateClient(FakeCreateOrderApiClient api, bool allowAutoRedirect = true) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICreateOrderApiClient>();
                services.AddSingleton<ICreateOrderApiClient>(api);
            }))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });

    private static async Task<Dictionary<string, string>> SignInAsync(HttpClient client, string roles = "Participant", bool canOrderOnline = true)
    {
        var cookies = new Dictionary<string, string>();
        var url = $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString(roles)}&participantId={Guid.NewGuid()}"
            + (canOrderOnline ? "&canOrderOnline=true" : string.Empty);
        CookieForwardingHttpClient.CaptureCookies(await client.GetAsync(url), cookies);
        return cookies;
    }

    private static async Task<string> GetAsync(HttpClient client, Dictionary<string, string> cookies, string url)
    {
        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, url, cookies);
        CookieForwardingHttpClient.CaptureCookies(response, cookies);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, Dictionary<string, string> cookies, string url) =>
        AntiforgeryTokenRegex().Match(await GetAsync(client, cookies, url)).Groups[1].Value;

    [Fact]
    public async Task CurrentYear_Participant_RendersTheSchemeGridWithBothSections()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Order/CurrentYear", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Create new order", body);
        Assert.Contains("2026/27", body);
        Assert.Contains("QAL/00001", body);
        Assert.Contains("Schemes you participated in last year and/or schemes you are participating in year 2026/27", body);
        Assert.Contains("2026/27&#x27;s commercially available schemes", body);
        Assert.Contains("Chemistry", body);
        Assert.Contains("Microbiology", body);
        Assert.Contains("Monthly", body);
        foreach (var column in new[] { "Select", "Scheme code", "Scheme name", "Annual price", "Import permit required", "Postage", "Scheme price", "Total" })
        {
            Assert.Contains($">{column}</th>", body);
        }

        Assert.Contains("&#xA3;100.00", body);
        Assert.Contains("id=\"create-order-body\"", body);
        Assert.Contains("data-year=\"current\"", body);
        Assert.Contains("/js/create-order.js", body);
        Assert.Contains("Order totals", body);
        Assert.Matches(AntiforgeryTokenPattern, body);
    }

    [Fact]
    public async Task NextYear_Participant_RendersForTheNextYear()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/NextYear");

        Assert.Contains("data-year=\"next\"", body);
    }

    [Fact]
    public async Task CurrentYear_SelectedScheme_ShowsMonthStripPricesAndEnabledNext()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true, participatingSelected: true)) };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear");

        Assert.Contains("Distribution months for Microbiology", body);
        Assert.Contains("&#xA3;30.00", body);
        Assert.Contains("&#xA3;20.00", body);
        Assert.Contains("&#xA3;10.00", body);
        Assert.DoesNotMatch(NextButtonDisabledPattern, body);
        // The month the participant already holds is ticked and locked; unoffered months are plain text.
        Assert.Matches("id=\"month-" + FakeCreateOrderApiClient.ParticipatingSchemeId + "-4\"[^>]*checked[^>]*disabled|id=\"month-" + FakeCreateOrderApiClient.ParticipatingSchemeId + "-4\"[^>]*disabled[^>]*checked", body);
        Assert.Contains("app-month--unavailable", body);
    }

    [Fact]
    public async Task CurrentYear_NothingSelected_NextButtonIsDisabled()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear");

        Assert.Matches(NextButtonDisabledPattern, body);
        Assert.Matches("id=\"create-order-error\"[^>]*\\shidden", body);
        Assert.Matches("<form[^>]*govuk-button-group[^>]*>(?:(?!</form>).)*Next(?:(?!</form>).)*Clear form(?:(?!</form>).)*Cancel", body.Replace("\r", " ").Replace("\n", " "));
    }

    [Fact]
    public async Task CurrentYear_SearchWithNoMatch_ShowsNoMatchMessageAndShowAllLink()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear?search=zzz");

        Assert.Contains("No schemes match your search.", body);
        Assert.Contains("Show all", body);
        Assert.DoesNotContain("Microbiology", body);
        Assert.Contains("Chemistry", body);
    }

    [Fact]
    public async Task CurrentYear_SearchMatchingOneScheme_FiltersTheCommercialList()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear?search=micro");

        Assert.Contains("Microbiology", body);
        Assert.DoesNotContain("No schemes match your search.", body);
    }

    [Fact]
    public async Task CurrentYear_NotAllowed_ExplainsWhyWithForbiddenStatus()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.NotAllowed) };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Order/CurrentYear", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("You cannot create an order for the current year", body);
        Assert.Contains("already been submitted", body);
    }

    [Fact]
    public async Task CurrentYear_ViewerOnlyUser_IsForbidden()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client, roles: "Viewer", canOrderOnline: false);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Order/CurrentYear", cookies);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CurrentYear_Anonymous_IsChallenged()
    {
        var client = CreateClient(new FakeCreateOrderApiClient(), allowAutoRedirect: false);

        var response = await client.GetAsync("/Order/CurrentYear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Change_Post_WithAntiforgeryToken_ReturnsOnlyTheRepricedBodyFragment()
    {
        var api = new FakeCreateOrderApiClient();
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);
        var token = await GetAntiforgeryTokenAsync(client, cookies, "/Order/CurrentYear");

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Post, "/Order/Change", cookies, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["year"] = "current",
            ["search"] = "",
            ["schemeId"] = FakeCreateOrderApiClient.AvailableSchemeId.ToString(),
            ["field"] = "select",
            ["checked"] = "true"
        }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Order totals", body);
        Assert.Contains("Distribution months for Microbiology", body);
        Assert.Equal("select", api.LastChange!.Field);
        Assert.True(api.LastChange.Checked);
        Assert.Equal(FakeCreateOrderApiClient.AvailableSchemeId, api.LastChange.SchemeId);
    }

    [Fact]
    public async Task Change_Post_WithoutSchemeIdOrChecked_IsRejectedAsUnderPosted()
    {
        var api = new FakeCreateOrderApiClient();
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);
        var token = await GetAntiforgeryTokenAsync(client, cookies, "/Order/CurrentYear");

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Post, "/Order/Change", cookies, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["year"] = "current",
            ["field"] = "select"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(api.LastChange);
    }

    [Fact]
    public async Task Change_Post_WithoutAntiforgeryToken_IsRejected()
    {
        var api = new FakeCreateOrderApiClient();
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Post, "/Order/Change", cookies, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["year"] = "current",
            ["schemeId"] = FakeCreateOrderApiClient.AvailableSchemeId.ToString(),
            ["field"] = "select",
            ["checked"] = "true"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(api.LastChange);
    }

    [Fact]
    public async Task ConfirmClear_RendersYesNoChoice()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/ConfirmClear?year=current");

        Assert.Contains("Are you sure you want to clear the form?", body);
        Assert.Contains("Yes, clear the form", body);
        Assert.Contains("No, go back to the order", body);
    }

    [Fact]
    public async Task Clear_Post_ClearsTheDraftAndRedirectsToTheYearPage()
    {
        var api = new FakeCreateOrderApiClient();
        var client = CreateClient(api, allowAutoRedirect: false);
        var cookies = await SignInAsync(client);
        var token = await GetAntiforgeryTokenAsync(client, cookies, "/Order/ConfirmClear?year=next");

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Post, "/Order/Clear", cookies, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["year"] = "next"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Order/NextYear", response.Headers.Location!.ToString());
        Assert.Equal("next", api.LastClearYear);
    }

    [Fact]
    public async Task Review_WithSelection_RendersReadOnlySummary()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true)) };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/Review?year=current");

        Assert.Contains("Pending order", body);
        Assert.Contains("Microbiology", body);
        Assert.Contains("Apr, May", body);
        Assert.Contains("Total amount", body);
        Assert.Contains("&#xA3;30.00", body);
        Assert.DoesNotContain("Chemistry", body);
    }

    [Fact]
    public async Task Review_NothingSelected_RedirectsBackToTheOrderPage()
    {
        var client = CreateClient(new FakeCreateOrderApiClient(), allowAutoRedirect: false);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Order/Review?year=current", cookies);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Order/CurrentYear", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Home_ParticipantWhoCanOrder_SeesBothYearCards()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Home/Index");

        Assert.Contains("Create order for current year", body);
        Assert.Contains("Create order for next year", body);
        Assert.Contains("/Order/CurrentYear", body);
        Assert.Contains("/Order/NextYear", body);
    }

    [Fact]
    public async Task Home_OrderAlreadySubmittedForCurrentYear_HidesThatYearsCardOnly()
    {
        var api = new FakeCreateOrderApiClient { Eligibility = new(true, false, true) };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var homeBody = await GetAsync(client, cookies, "/Home/Index");

        // The header menu stays claim-based and lists both links; only the Home card for the submitted year disappears.
        var currentYearMentions = Regex.Count(homeBody, "Create order for current year");
        var nextYearMentions = Regex.Count(homeBody, "Create order for next year");
        Assert.True(nextYearMentions > 0);
        Assert.Equal(nextYearMentions - 1, currentYearMentions);
    }

    [Fact]
    public async Task OrderPages_BreadcrumbsNameTheOrdersSection()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var yearPage = await GetAsync(client, cookies, "/Order/CurrentYear");
        var confirmPage = await GetAsync(client, cookies, "/Order/ConfirmClear?year=current");

        Assert.Contains(">Orders</li>", yearPage);
        Assert.Contains(">Orders</li>", confirmPage);
    }

    private const string AntiforgeryTokenPattern = "__RequestVerificationToken[^>]*value=\"[^\"]+\"";

    private const string NextButtonDisabledPattern = "<button[^>]*\\sdisabled=\"disabled\"[^>]*>\\s*Next\\s*</button>";

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
