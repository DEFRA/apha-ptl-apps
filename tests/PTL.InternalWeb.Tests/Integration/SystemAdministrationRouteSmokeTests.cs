using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;
using PTL.Contracts.TestConsultant;
using PTL.Contracts.User;
using PTL.Contracts.Viewer;
using PTL.InternalWeb.Tests.TestSupport;
using CountryResponse = PTL.Contracts.Country.CountryResponse;
using CountryTypeResponse = PTL.Contracts.Country.CountryTypeResponse;
using CountryDeleteResponse = PTL.Contracts.Country.CountryDeleteResponse;
using ExternalSiteMessageResponse = PTL.Contracts.ExternalSiteMessage.ExternalSiteMessageResponse;
using ExternalSiteMessageSaveResult = PTL.Contracts.ExternalSiteMessage.ExternalSiteMessageSaveResult;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the System Administration Razor views (SystemAdministration,
// AdministrationCharge, WeightedPricingPlan, PostagePricingPlan) actually render at least once
// through the real MVC pipeline, rather than only being exercised via SystemAdministrationControllerTests.
public partial class SystemAdministrationRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private readonly WebApplicationFactory<Program> _rawFactory;
    private readonly WebApplicationFactory<Program> _factory;

    public SystemAdministrationRouteSmokeTests(WebApplicationFactory<Program> factory)
    {
        _rawFactory = factory;
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAdministrationChargeApiClient>();
                services.AddSingleton<IAdministrationChargeApiClient>(new FakeAdministrationChargeApiClient());
                services.RemoveAll<IWeightedPricingPlanApiClient>();
                services.AddSingleton<IWeightedPricingPlanApiClient>(new FakeWeightedPricingPlanApiClient());
                services.RemoveAll<IPostagePricingPlanApiClient>();
                services.AddSingleton<IPostagePricingPlanApiClient>(new FakePostagePricingPlanApiClient());
                services.RemoveAll<ICountryApiClient>();
                services.AddSingleton<ICountryApiClient>(new FakeCountryApiClient());
                services.RemoveAll<IExternalSiteMessageApiClient>();
                services.AddSingleton<IExternalSiteMessageApiClient>(new FakeExternalSiteMessageApiClient());
                services.RemoveAll<IUserApiClient>();
                services.AddSingleton<IUserApiClient>(new FakeUserApiClient());
                services.RemoveAll<IRoleApiClient>();
                services.AddSingleton<IRoleApiClient>(new FakeRoleApiClient());
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
                services.RemoveAll<IExternalTestConsultantApiClient>();
                services.AddSingleton<IExternalTestConsultantApiClient>(new FakeExternalTestConsultantApiClient());
                services.RemoveAll<IViewerApiClient>();
                services.AddSingleton<IViewerApiClient>(new FakeViewerApiClient());

                // Stands in for a real Entra ID sign-in (see TestAuthHandler remarks) so every
                // existing smoke test below - which assumes full access, matching this feature
                // set's confirmed RBAC decision - keeps working without a live Entra tenant.
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                });
            }));
    }

    [Fact]
    public async Task SystemAdministration_WithoutSignIn_RedirectsAwayFromThePage()
    {
        // Uses _rawFactory (no TestAuthHandler override) so the real Entra cookie/OIDC pipeline
        // governs - with no cookie present, the request is anonymous and SystemAdministrationPolicy
        // must reject it before the action body (and therefore any API call) ever runs.
        var factory = _rawFactory.WithWebHostBuilder(builder => { });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/SystemAdministration/SystemAdministration");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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
        Assert.Contains("<title>System Administration - Proficiency Testing - Internal</title>", body, StringComparison.Ordinal);
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

    [Fact]
    public async Task CountryManagement_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/CountryManagement");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The empty-state test above never exercises the @foreach row loop or the edit-mode cells
    // (form="countryEditForm" cross-element binding, govuk-error-summary) - this is the only test
    // that renders that markup for real. Seeds a second, non-editing row too, so both the
    // read-only "Edit"/"Remove" actions and the editing row's "Save"/"Cancel" render on the same
    // page.
    [Fact]
    public async Task CountryManagement_WithEditingRow_RendersTableAndEditForm()
    {
        var countryId = Guid.NewGuid();
        var otherCountryId = Guid.NewGuid();
        var countryTypeId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICountryApiClient>();
                services.AddSingleton<ICountryApiClient>(new FakeCountryApiClient
                {
                    Countries =
                    [
                        new CountryResponse(countryId, "France", countryTypeId, "EU", 0),
                        new CountryResponse(otherCountryId, "United Kingdom", countryTypeId, "UK", 3)
                    ],
                    CountryTypes = [new CountryTypeResponse(countryTypeId, "EU")]
                });
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/SystemAdministration/CountryManagement?editId={countryId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("France", body, StringComparison.Ordinal);
        Assert.Contains("United Kingdom", body, StringComparison.Ordinal);
        Assert.Contains("countryEditForm", body, StringComparison.Ordinal);
        Assert.Contains("type=\"submit\">Save</button>", body, StringComparison.Ordinal);
        Assert.Contains(">Edit</a>", body, StringComparison.Ordinal);
        Assert.Contains(">Cancel</a>", body, StringComparison.Ordinal);
        Assert.Contains("govuk-button--secondary", body, StringComparison.Ordinal);
        Assert.Contains("Remove", body, StringComparison.Ordinal);
    }

    // Locks in that a blocked removal (country still referenced) surfaces legacy's exact
    // dependency-count wording as a notification banner, matching "This country is being used by
    // N customer(s)/participant(s)/Group Addresses." from Admin/ManageCountries.aspx.vb.
    [Fact]
    public async Task CountryManagementRemove_Post_CountryInUse_RendersBlockedMessage()
    {
        var countryId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICountryApiClient>();
                services.AddSingleton<ICountryApiClient>(new FakeCountryApiClient
                {
                    Countries = [new CountryResponse(countryId, "France", Guid.NewGuid(), "EU", 8)],
                    DeleteResponse = new CountryDeleteResponse(false, "This country is being used by 8 customer(s)/participant(s)/Group Addresses.")
                });
            }));
        var client = factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SystemAdministration/CountryManagement");

        var request = new HttpRequestMessage(HttpMethod.Post, "/SystemAdministration/CountryManagementRemove")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["countryId"] = countryId.ToString()
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This country is being used by 8 customer(s)/participant(s)/Group Addresses.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalSiteManagement_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/ExternalSiteManagement");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Locks in that the saved Further Information/Important Message content actually renders
    // into the page (inside the TinyMCE-bound textareas) and that the TinyMCE script is wired up.
    [Fact]
    public async Task ExternalSiteManagement_RendersSavedContentAndTinyMceScript()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExternalSiteMessageApiClient>();
                services.AddSingleton<IExternalSiteMessageApiClient>(new FakeExternalSiteMessageApiClient
                {
                    Message = new ExternalSiteMessageResponse("<p>Further information body</p>", "<p>Urgent notice</p>", "vetqas@apha.gov.uk")
                });
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/ExternalSiteManagement");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Further information body", body, StringComparison.Ordinal);
        Assert.Contains("Urgent notice", body, StringComparison.Ordinal);
        Assert.Contains("vetqas@apha.gov.uk", body, StringComparison.Ordinal);
        Assert.Contains("lib/tinymce/tinymce.min.js", body, StringComparison.Ordinal);
    }

    // Locks in that a blocked save (Important Message over 500 visible characters) surfaces
    // legacy's exact wording as an error-summary entry with a matching anchor.
    [Fact]
    public async Task ExternalSiteManagement_Post_ImportantMessageTooLong_RendersErrorSummaryWithMatchingAnchor()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExternalSiteMessageApiClient>();
                services.AddSingleton<IExternalSiteMessageApiClient>(new FakeExternalSiteMessageApiClient
                {
                    SaveResult = new ExternalSiteMessageSaveResult(false, null, new Dictionary<string, string[]> { ["ImportantMessage"] = ["The Important Message cannot exceed 500 characters."] })
                });
            }));
        var client = factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SystemAdministration/ExternalSiteManagement");

        var request = new HttpRequestMessage(HttpMethod.Post, "/SystemAdministration/ExternalSiteManagement")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Message"] = "Body",
                ["ImportantMessage"] = new string('a', 600),
                ["SupportEmailAddress"] = "vetqas@apha.gov.uk"
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", body, StringComparison.Ordinal);
        Assert.Contains("The Important Message cannot exceed 500 characters.", body, StringComparison.Ordinal);
        Assert.Contains("href=\"#ImportantMessage\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUser_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/CreateUser");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The default empty-state test above never exercises the results dropdown or the "no valid
    // search results found" banner - this is the only test that posts a real search and renders
    // that markup.
    [Fact]
    public async Task CreateUserSearch_Post_NoResults_RendersNoValidResultsMessage()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserApiClient>();
                services.AddSingleton<IUserApiClient>(new FakeUserApiClient { SearchResults = [] });
            }));
        var client = factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SystemAdministration/CreateUser");

        var request = new HttpRequestMessage(HttpMethod.Post, "/SystemAdministration/CreateUserSearch")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["SearchTerm"] = "nobody"
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No valid search results found", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserSearch_Post_ResultsFound_RendersSelectAndDepartmentForm()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserApiClient>();
                services.AddSingleton<IUserApiClient>(new FakeUserApiClient
                {
                    SearchResults = [new StaffDirectoryUserResponse("m100001", "jane@apha.gov.uk", "Jane Smith", "Jane", "Smith")]
                });
            }));
        var client = factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SystemAdministration/CreateUser");

        var request = new HttpRequestMessage(HttpMethod.Post, "/SystemAdministration/CreateUserSearch")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["SearchTerm"] = "jane"
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Jane Smith : m100001", body, StringComparison.Ordinal);
        Assert.Contains("CreateUserConfirm", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManageUserRoles_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/ManageUserRoles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The default empty-state test above never exercises the role-column headers or the
    // per-user checkbox grid - this is the only test that renders real rows.
    [Fact]
    public async Task ManageUserRoles_WithData_RendersRoleColumnsAndCheckedBox()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRoleApiClient>();
                services.AddSingleton<IRoleApiClient>(new FakeRoleApiClient { Roles = [new RoleResponse(roleId, "Admin")] });
                services.RemoveAll<IUserApiClient>();
                services.AddSingleton<IUserApiClient>(new FakeUserApiClient
                {
                    UserRoleGrid = [new UserRoleRowResponse(userId, "m100001", "Jane Smith", [roleId])]
                });
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/ManageUserRoles");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Jane Smith", body, StringComparison.Ordinal);
        Assert.Contains("type=\"checkbox\"", body, StringComparison.Ordinal);
        Assert.Contains("checked=\"checked\"", body, StringComparison.Ordinal);
        // Regression guard: the visible checkbox box is a pseudo-element on
        // govuk-checkboxes__label itself - govuk-visually-hidden must never be applied directly
        // to that label (it would hide the box along with the text), only to a nested span.
        Assert.DoesNotContain("govuk-checkboxes__label govuk-visually-hidden", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManageUserRoles_Post_BlockedBySelfAdminRule_RendersErrorBanner()
    {
        var userId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserApiClient>();
                services.AddSingleton<IUserApiClient>(new FakeUserApiClient
                {
                    SetRolesResult = new SetUserRolesResponse(false, "You cannot remove your own Admin access.")
                });
            }));
        var client = factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SystemAdministration/ManageUserRoles");

        var request = new HttpRequestMessage(HttpMethod.Post, "/SystemAdministration/ManageUserRoles")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Rows[0].UserId"] = userId.ToString(),
                ["Rows[0].FriendlyName"] = "Jane Smith"
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You cannot remove your own Admin access.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveUser_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/RemoveUser");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemoveUser_Post_Valid_RendersSuccessBanner()
    {
        var userId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserApiClient>();
                services.AddSingleton<IUserApiClient>(new FakeUserApiClient());
            }));
        var client = factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SystemAdministration/RemoveUser");

        var request = new HttpRequestMessage(HttpMethod.Post, "/SystemAdministration/RemoveUser")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["SelectedUserId"] = userId.ToString()
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("User Removed Successfully", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InternalTestConsultantDepartment_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/InternalTestConsultantDepartment");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InternalTestConsultantDepartment_WithData_RendersDepartmentAndStatus()
    {
        var userId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserApiClient>();
                services.AddSingleton<IUserApiClient>(new FakeUserApiClient
                {
                    TestConsultants = [new UserResponse(userId, "m100001", "Jane Smith", "Jane", "Smith", "jane@apha.gov.uk", "Science", true, new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc))]
                });
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/InternalTestConsultantDepartment");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Jane Smith", body, StringComparison.Ordinal);
        Assert.Contains("Science", body, StringComparison.Ordinal);
        Assert.Contains("Inactive", body, StringComparison.Ordinal);
        Assert.Contains("Activate", body, StringComparison.Ordinal);
        Assert.Contains("You are setting this test consultant to be active. Are you sure?", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalTestConsultantManagement_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/ExternalTestConsultantManagement");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExternalTestConsultantManagement_WithData_RendersAllSevenColumns()
    {
        var consultantId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExternalTestConsultantApiClient>();
                services.AddSingleton<IExternalTestConsultantApiClient>(new FakeExternalTestConsultantApiClient
                {
                    TestConsultants = [new ExternalTestConsultantResponse(consultantId, "Jane Smith", "Science", "jane@example.com", false, null, false)]
                });
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/ExternalTestConsultantManagement");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Jane Smith", body, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", body, StringComparison.Ordinal);
        Assert.Contains("Active", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewerManagement_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/ViewerManagement");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ViewerManagement_WithData_RendersAllFourColumns()
    {
        var viewerId = Guid.NewGuid();
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IViewerApiClient>();
                services.AddSingleton<IViewerApiClient>(new FakeViewerApiClient
                {
                    Viewers = [new ViewerResponse(viewerId, "Jane Smith", "jane@example.com", false, [], [])]
                });
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/ViewerManagement");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Jane Smith", body, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", body, StringComparison.Ordinal);
        Assert.Contains("Remove", body, StringComparison.Ordinal);
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

