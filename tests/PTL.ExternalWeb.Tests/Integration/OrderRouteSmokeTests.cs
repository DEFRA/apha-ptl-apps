using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.CreateOrder;
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
        Assert.Contains("For price-lists, including distribution dates, please refer to our", body);
        Assert.Contains("To participate in a new scheme(s)", body);
        Assert.Contains("you are unable to cancel existing orders from this page", body);
        Assert.Contains("href=\"mailto:vetqas@apha.gov.uk\"", body);
        Assert.Contains("2026/27", body);
        Assert.Contains("QAL/00001", body);
        Assert.Contains("Schemes you participated in last year and/or schemes you are participating in year 2026/27", body);
        Assert.Contains("2026/27&#x27;s commercially available schemes", body);
        Assert.Contains("Chemistry", body);
        Assert.Contains("Microbiology", body);
        Assert.Contains("Monthly", body);
        foreach (var column in new[] { "Select", "Annual price", "Import permit required", "Postage", "Scheme price", "Total" })
        {
            Assert.Contains($">{column}</th>", body);
        }

        // Code and name are sortable column links; nothing is sorted until one is clicked.
        Assert.Contains("aria-sort=\"none\"", body);
        Assert.Contains(">Scheme code</a>", body);
        Assert.Contains(">Scheme name</a>", body);
        Assert.DoesNotContain("aria-sort=\"ascending\"", body);

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

    private static CreateOrderSchemeResponse AvailableScheme(string identifier, string name) =>
        new(Guid.NewGuid(), identifier, name, "Monthly", 100m, false, false, true, 0m, 0m, 0m, false, []);

    private static FakeCreateOrderApiClient ApiWithTwoMonthlySchemes() => new()
    {
        GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder() with
        {
            AvailableSchemes = [new CreateOrderScheduleGroupResponse("Monthly", [AvailableScheme("PT300", "Zulu"), AvailableScheme("PT100", "Alpha")])]
        })
    };

    [Fact]
    public async Task CurrentYear_SortedByName_OrdersThatTableAndLinksToTheOppositeDirection()
    {
        var client = CreateClient(ApiWithTwoMonthlySchemes());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear?sort=name%7Casc%7Cschedule%3AMonthly");

        Assert.True(body.IndexOf("Alpha", StringComparison.Ordinal) < body.IndexOf("Zulu", StringComparison.Ordinal));
        Assert.Contains("aria-sort=\"ascending\"", body);
        Assert.Contains("sort=name%7Cdesc%7Cschedule%3AMonthly", body);
        Assert.Contains("sorted ascending", body);
    }

    [Fact]
    public async Task CurrentYear_SortedDescending_ReversesTheTable()
    {
        var client = CreateClient(ApiWithTwoMonthlySchemes());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear?sort=code%7Cdesc%7Cschedule%3AMonthly");

        Assert.True(body.IndexOf("Zulu", StringComparison.Ordinal) < body.IndexOf("Alpha", StringComparison.Ordinal));
        Assert.Contains("aria-sort=\"descending\"", body);
    }

    [Fact]
    public async Task CurrentYear_SortChoices_SurviveASearchAndTheChangeScript()
    {
        var client = CreateClient(ApiWithTwoMonthlySchemes());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear?sort=name%7Casc%7Cschedule%3AMonthly&search=a");

        Assert.Matches("name=\"sort\"\\s+value=\"name", body);
        Assert.Contains("data-sort=", body);
        Assert.Contains("schedule:Monthly", body.Replace("&#x3A;", ":").Replace("&#x7C;", "|"));
    }

    [Fact]
    public async Task Review_SummaryStartsOnSchemeNameAscending_AndHonoursAChosenSort()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true)) };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var initial = await GetAsync(client, cookies, "/Order/Review?year=current");
        var chosen = await GetAsync(client, cookies, "/Order/Review?year=current&sort=code%7Cdesc%7Csummary");

        Assert.Contains("aria-sort=\"ascending\"", initial);
        Assert.Contains(">Scheme code</a>", initial);
        Assert.Contains("aria-sort=\"descending\"", chosen);
        Assert.Matches("name=\"Sort\"\\s+value=\"code", chosen);
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
        // As in legacy, each month's name sits under its checkbox (a second, aria-hidden label); the checkbox keeps one accessible name.
        Assert.Matches("<label class=\"app-month__name\" for=\"month-[^\"]+\" aria-hidden=\"true\">Apr</label>", body);
    }

    [Fact]
    public async Task CurrentYear_NothingSelected_NextButtonIsDisabled()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear");

        Assert.Matches(NextButtonDisabledPattern, body);
        Assert.Contains("Select at least one scheme and at least one distribution month for it to continue.", body);
        Assert.Matches("id=\"create-order-error\"[^>]*\\shidden", body);
        Assert.Matches("<form[^>]*govuk-button-group[^>]*>(?:(?!</form>).)*Next(?:(?!</form>).)*Back(?:(?!</form>).)*Clear form", body.Replace("\r", " ").Replace("\n", " "));
    }

    [Fact]
    public async Task CurrentYear_SearchWithNoMatch_ShowsNoMatchMessageAndShowAllLink()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear?search=zzz");

        Assert.Contains("No schemes match your search.", body);
        Assert.Contains("Show All</a>", body);
        Assert.DoesNotContain("Microbiology", body);
        Assert.Contains("Chemistry", body);
    }

    [Fact]
    public async Task CurrentYear_UnfilteredPage_AlsoShowsShowAllButton()
    {
        var client = CreateClient(new FakeCreateOrderApiClient());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear");

        Assert.Contains("Show All</a>", body);
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
        Assert.Contains("For price-lists, including distribution dates, please refer to our", body);
        Assert.Contains("QAL number", body);
        Assert.Contains("Lab code", body);
        Assert.Contains("Full price", body);
        Assert.DoesNotContain("Annual price", body);
        Assert.Contains("Microbiology - (Monthly)", body);
        Assert.Contains("Apr<span class=\"govuk-visually-hidden\"> (selected)</span>", body);
        Assert.Contains("May<span class=\"govuk-visually-hidden\"> (selected)</span>", body);
        Assert.Contains("Jun<span class=\"govuk-visually-hidden\"> (not selected)</span>", body);
        Assert.Contains("Total amount", body);
        Assert.Contains("&#xA3;30.00", body);
        Assert.DoesNotContain("Chemistry", body);
    }

    [Fact]
    public async Task Review_ShowsAMonthTheParticipantAlreadyHoldsAsGrey()
    {
        var order = FakeCreateOrderApiClient.SampleOrder(selected: true, participatingSelected: true);
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, order) };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/Review?year=current");

        Assert.Contains("govuk-tag govuk-tag--grey", body);
        Assert.Contains("(you are already participating in this month)", body);
    }

    [Fact]
    public async Task Review_FollowsTheLegacyOrderOfOrderDetailsAndButtons()
    {
        var client = CreateClient(ReviewApi());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/Review?year=current");

        static void InOrder(string body, params string[] parts)
        {
            var last = -1;
            foreach (var part in parts)
            {
                var index = body.IndexOf(part, last + 1, StringComparison.Ordinal);
                Assert.True(index > last, $"Expected '{part}' after the previous item.");
                last = index;
            }
        }

        InOrder(body, "Order details", "Contract year", "Purchase order number", "Scheme price", "Combined postage", "Total amount");
        InOrder(body, ">Submit</button>", ">Previous</a>", ">Save</button>", ">Print</a>");
        Assert.Contains("You must agree to your data being shared for the schemes listed below before you can proceed with your order.", body);
        Assert.Contains("PT200: Microbiology", body);
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
    public async Task Print_WithSelection_RendersPrintableSummary()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true)) };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/Print?year=current");

        Assert.Contains("<h1 class=\"govuk-heading-xl\">Pending order</h1>", body);
        Assert.Contains("Contract year", body);
        Assert.Contains("Purchase order number", body);
        Assert.Contains("Not provided", body);
        Assert.Contains("Microbiology", body);
        Assert.Contains("Combined postage (", body);
        Assert.Contains("(Courier or Bio-Freeze)", body);
        Assert.Contains("Total amount (", body);
        Assert.Contains("Print this page", body);
        Assert.DoesNotContain("Submit", body);
    }

    [Fact]
    public async Task Print_FollowsTheLegacyPendingOrderPrintout_LogoDetailsMonthStripAndFooter()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true)) };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/Print?year=current");

        // Print-only identity: the legacy logo block and copyright line (the app header and footer are hidden in print).
        Assert.Contains("class=\"app-print-only app-print-logo\" src=\"/images/vetqasheader.png\" alt=\"VETQAS and APHA Scientific\"", body);
        Assert.Matches("class=\"govuk-body-s app-print-only app-print-footer\">&copy; Copyright \\d{4}, Animal and Plant Health Agency", body);
        Assert.Contains("For price-lists, including distribution dates, please refer to our", body);
        Assert.Contains("QAL/00001", body);
        Assert.Contains("LAB001", body);
        Assert.Contains("Schemes you have selected", body);
        Assert.Contains(">Full price</th>", body);
        Assert.Contains("Order details", body);

        // Months are a 12-cell strip with a text tick and a spoken state, not colour alone.
        Assert.Contains("aria-label=\"Distribution months for Microbiology\"", body);
        Assert.Contains("app-print-month--selected", body);
        Assert.Contains("(in this order)", body);
        Assert.Contains("(not in this order)", body);
        Assert.Equal(12, Regex.Count(body, "<li class=\"app-print-month[ \"]"));
    }

    [Fact]
    public async Task Print_ShowsTheConsentsTermsLinkAndTheConfiguredPriceListLink()
    {
        var client = CreateClient(ReviewApi());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/Print?year=current");

        Assert.Contains("Data consent", body);
        Assert.Contains("I agree to share my data", body);
        Assert.Contains("Not yet agreed", body);
        Assert.Contains("Terms and conditions:", body);
    }

    private static CreateOrderResponse ReviewOrder() => FakeCreateOrderApiClient.SampleOrder(selected: true) with
    {
        PurchaseOrderNumber = "PO-1",
        Consents = [new CreateOrderConsentResponse(FakeCreateOrderApiClient.AvailableSchemeId, "PT200", "Microbiology", "I agree to share my data", false)]
    };

    private static FakeCreateOrderApiClient ReviewApi() => new() { GetResult = new(CreateOrderApiStatus.Ok, ReviewOrder()) };

    [Fact]
    public async Task Review_RendersThePurchaseOrderFieldTermsConsentAndButtons()
    {
        var client = CreateClient(ReviewApi());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/Review?year=current");

        Assert.Contains("Schemes you have selected", body);
        Assert.Contains("Order details", body);
        Assert.Contains("Contract year", body);
        Assert.Contains("name=\"PurchaseOrderNumber\"", body);
        Assert.Contains("value=\"PO-1\"", body);
        Assert.Contains("maxlength=\"255\"", body);
        Assert.Contains("View terms and conditions (opens in new tab)", body);
        Assert.Contains("target=\"_blank\"", body);
        Assert.Contains("I have read and agreed to the terms and conditions", body);
        Assert.Contains("I agree to share my data", body);
        Assert.Contains("name=\"ConsentedSchemeIds\"", body);
        Assert.Contains("id=\"submit-order\"", body);
        Assert.Contains("formaction=\"/Order/Save\"", body);
        Assert.Contains(">Save</button>", body);
        Assert.Contains(">Print</a>", body);
        Assert.Contains(">Previous</a>", body);
        Assert.Contains("/js/review-order.js", body);
        Assert.Matches(AntiforgeryTokenPattern, body);
        Assert.DoesNotContain("not yet available online", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Review_TermsLinkOpensTheTermsPdfShippedWithTheApp()
    {
        var client = CreateClient(ReviewApi());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/Review?year=current");

        const string href = "/documents/VETQAS%20Ts%20%26%20Cs%20April%202025%20Version%206%201.pdf";
        Assert.Contains($"href=\"{href}\"", body);

        var pdf = await client.GetAsync(href);

        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Review_WithoutConsentsOrATermsAddress_HidesBothSections()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true)) };
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["CreateOrder:TermsAndConditionsUrl"] = "" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICreateOrderApiClient>();
                services.AddSingleton<ICreateOrderApiClient>(api);
            });
        }).CreateClient();
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/Review?year=current");

        Assert.DoesNotContain("View terms and conditions", body);
        Assert.DoesNotContain("ConsentedSchemeIds", body);
        Assert.Contains("I have read and agreed to the terms and conditions", body);
    }

    private static async Task<HttpResponseMessage> PostReviewFormAsync(HttpClient client, Dictionary<string, string> cookies, string action, params (string Key, string Value)[] fields)
    {
        var token = await GetAntiforgeryTokenAsync(client, cookies, "/Order/Review?year=current");
        var form = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token), new("Year", "current") };
        form.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));
        return await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Post, $"/Order/{action}", cookies, new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task Submit_Post_WithTheTermsAccepted_SubmitsAndGoesHome()
    {
        var api = ReviewApi();
        var client = CreateClient(api, allowAutoRedirect: false);
        var cookies = await SignInAsync(client);

        var response = await PostReviewFormAsync(client, cookies, "Submit",
            ("PurchaseOrderNumber", "PO-9"), ("TermsAccepted", "true"), ("ConsentedSchemeIds", FakeCreateOrderApiClient.AvailableSchemeId.ToString()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Home", response.Headers.Location!.ToString());
        Assert.Equal("PO-9", api.LastSubmit!.PurchaseOrderNumber);
        Assert.True(api.LastSubmit.TermsAccepted);
        Assert.Equal([FakeCreateOrderApiClient.AvailableSchemeId], api.LastSubmit.ConsentedSchemeIds);
    }

    [Fact]
    public async Task Submit_Post_WhenTheServerRejectsIt_ShowsTheErrorSummaryAndKeepsWhatWasTyped()
    {
        var api = ReviewApi();
        api.SubmitResult = new(CreateOrderApiStatus.Ok,
        [
            new CreateOrderValidationErrorResponse("TermsAccepted", "Select that you have read the terms"),
            new CreateOrderValidationErrorResponse("PurchaseOrderNumber", "The purchase order number is too long")
        ]);
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var response = await PostReviewFormAsync(client, cookies, "Submit", ("PurchaseOrderNumber", "PO-TYPED"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("There is a problem", body);
        Assert.Contains("<title>Error: Pending order", body);
        Assert.Contains("href=\"#TermsAccepted\"", body);
        Assert.Contains("href=\"#PurchaseOrderNumber\"", body);
        Assert.Contains("govuk-error-message", body);
        Assert.Contains("Select that you have read the terms", body);
        Assert.Contains("value=\"PO-TYPED\"", body);
    }

    [Fact]
    public async Task Save_Post_StoresThePurchaseOrderAndReturnsToTheReview()
    {
        var api = ReviewApi();
        var client = CreateClient(api, allowAutoRedirect: false);
        var cookies = await SignInAsync(client);

        var response = await PostReviewFormAsync(client, cookies, "Save", ("PurchaseOrderNumber", "PO-3"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Order/Review", response.Headers.Location!.ToString());
        Assert.Equal(new CreateOrderSaveRequest("PO-3"), api.LastSave);
    }

    [Theory]
    [InlineData("Save")]
    [InlineData("Submit")]
    public async Task SaveAndSubmit_Post_WithoutAntiforgeryToken_AreRejected(string action)
    {
        var api = ReviewApi();
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Post, $"/Order/{action}", cookies,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Year"] = "current", ["TermsAccepted"] = "true" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(api.LastSave);
        Assert.Null(api.LastSubmit);
    }

    [Fact]
    public async Task Print_ShowsTheSavedPurchaseOrderNumber()
    {
        var client = CreateClient(ReviewApi());
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/Print?year=current");

        Assert.Contains("PO-1", body);
        Assert.DoesNotContain("Not provided", body);
    }

    [Fact]
    public async Task Print_NothingSelected_RedirectsBackToTheOrderPage()
    {
        var client = CreateClient(new FakeCreateOrderApiClient(), allowAutoRedirect: false);
        var cookies = await SignInAsync(client);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Order/Print?year=current", cookies);

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
    public async Task Home_OrderAlreadySubmittedForCurrentYear_DisablesThatYearsCardAndShowsTheMessage()
    {
        var api = new FakeCreateOrderApiClient { Eligibility = new(true, false, true, "2026/27", "2027/28") };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var homeBody = await GetAsync(client, cookies, "/Home/Index");

        // The card stays, as in legacy, but is plain text with the submitted message; the next year's card is a normal link.
        Assert.Contains("app-card--disabled", homeBody);
        Assert.Contains("Order has been submitted for 2026/27.", homeBody);
        Assert.DoesNotContain("Order has been submitted for 2027/28", homeBody);
        Assert.DoesNotContain("app-card-link--header\" href=\"/Order/CurrentYear", homeBody);
        Assert.Contains("app-card-link--header\" href=\"/Order/NextYear", homeBody);
    }

    [Fact]
    public async Task Menu_OrderAlreadySubmittedForCurrentYear_HidesThatYearsLinkAndKeepsTheNextYears()
    {
        var api = new FakeCreateOrderApiClient { Eligibility = new(true, false, true, "2026/27", "2027/28") };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var homeBody = await GetAsync(client, cookies, "/Home/Index");

        // Legacy hid the menu link once submitted (the Home card is disabled instead).
        Assert.DoesNotMatch("app-menu-panel__link\"[^>]*>Create order for current year", homeBody);
        Assert.Matches("app-menu-panel__link\"[^>]*>Create order for next year", homeBody);
        Assert.Contains("Order has been submitted for 2026/27.", homeBody);
    }

    [Fact]
    public async Task Menu_OrderAlreadySubmittedForNextYear_HidesThatYearsLinkAndKeepsTheCurrentYears()
    {
        var api = new FakeCreateOrderApiClient { Eligibility = new(true, true, false, "2026/27", "2027/28") };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Order/CurrentYear");

        // The menu is on every page, not just Home.
        Assert.Matches("app-menu-panel__link\"[^>]*>Create order for current year", body);
        Assert.DoesNotMatch("app-menu-panel__link\"[^>]*>Create order for next year", body);
    }

    [Fact]
    public async Task Menu_EligibilityLookupFails_StillShowsBothYearLinks()
    {
        var api = new FakeCreateOrderApiClient { EligibilityException = new HttpRequestException("down") };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var body = await GetAsync(client, cookies, "/Home/Index");

        Assert.Matches("app-menu-panel__link\"[^>]*>Create order for current year", body);
        Assert.Matches("app-menu-panel__link\"[^>]*>Create order for next year", body);
    }

    [Fact]
    public async Task Home_OrderAlreadySubmittedForNextYear_DisablesThatYearsCardAndShowsTheMessage()
    {
        var api = new FakeCreateOrderApiClient { Eligibility = new(true, true, false, "2026/27", "2027/28") };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var homeBody = await GetAsync(client, cookies, "/Home/Index");

        Assert.Contains("Order has been submitted for 2027/28.", homeBody);
        Assert.DoesNotContain("app-card-link--header\" href=\"/Order/NextYear", homeBody);
        Assert.Contains("app-card-link--header\" href=\"/Order/CurrentYear", homeBody);
    }

    [Fact]
    public async Task OrderPages_BreadcrumbsFollowLegacy_HomeThenCreateNewOrderThenStage()
    {
        var api = new FakeCreateOrderApiClient { GetResult = new(CreateOrderApiStatus.Ok, FakeCreateOrderApiClient.SampleOrder(selected: true)) };
        var client = CreateClient(api);
        var cookies = await SignInAsync(client);

        var yearPage = await GetAsync(client, cookies, "/Order/CurrentYear");
        var reviewPage = await GetAsync(client, cookies, "/Order/Review?year=next");
        var confirmPage = await GetAsync(client, cookies, "/Order/ConfirmClear?year=current");

        Assert.Contains("aria-current=\"page\">Create new order</li>", yearPage);
        Assert.DoesNotContain(">Orders</li>", yearPage);

        Assert.Contains("href=\"/Order/NextYear\">Create new order</a>", reviewPage);
        Assert.Contains("aria-current=\"page\">Pending order</li>", reviewPage);

        Assert.Contains("href=\"/Order/CurrentYear\">Create new order</a>", confirmPage);
        Assert.Contains("aria-current=\"page\">Clear the order form</li>", confirmPage);
    }

    private const string AntiforgeryTokenPattern = "__RequestVerificationToken[^>]*value=\"[^\"]+\"";

    private const string NextButtonDisabledPattern = "<button[^>]*\\sdisabled=\"disabled\"[^>]*>\\s*Next\\s*</button>";

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
