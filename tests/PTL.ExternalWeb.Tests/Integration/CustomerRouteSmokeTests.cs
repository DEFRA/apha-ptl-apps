using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Customer;
using PTL.Contracts.Lookup;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// Full-pipeline smoke tests for the Edit Customer/Invoice Details screen (Story 744638, legacy
// EditCustomerDetails.aspx) - proves the Razor view actually renders through real routing/
// authorization, not just that the controller action returns a view result.
public partial class CustomerRouteSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public CustomerRouteSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    private static readonly CustomerDetailsEditResponse SampleDetails = new(
        "QAL/00001", false, "Jane Doe", "Test Lab", "1 Test Street", "Testville", "", "", "",
        Guid.NewGuid(), "01234 567890", "", "jane@example.com",
        "", "Test Lab Invoicing", "1 Test Street", "Testville", "", "", "",
        Guid.NewGuid(), "", "", "");

    private HttpClient CreateClient(
        CustomerDetailsEditResponse? details = null,
        CustomerDetailsEditSaveResult? submitResult = null,
        bool allowAutoRedirect = true) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICustomerDetailsEditApiClient>();
                services.AddSingleton<ICustomerDetailsEditApiClient>(new FakeCustomerDetailsEditApiClient
                {
                    Details = details ?? SampleDetails,
                    SubmitResult = submitResult ?? new CustomerDetailsEditSaveResult(true, new Dictionary<string, string[]>())
                });
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Countries = [new CountryResponse(Guid.NewGuid(), "United Kingdom")]
                });
            }))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });

    private static async Task<Dictionary<string, string>> SignInAsParticipantAsync(HttpClient client, Guid participantId)
    {
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync(
            $"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Participant")}&participantId={participantId}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);
        return cookies;
    }

    [Fact]
    public async Task Index_ParticipantSignedIn_RendersFormPrePopulatedWithCurrentDetails()
    {
        var client = CreateClient();
        var cookies = await SignInAsParticipantAsync(client, Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Customer/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("QAL/00001", body);
        Assert.Contains("Jane Doe", body);
        Assert.Contains("Test Lab Invoicing", body);
        // Invoice Country renders as a dropdown, not a text input.
        Assert.Contains("id=\"InvoiceCountryId\"", body);
        Assert.Contains("<select", body);
        // Contents section (AC4) still links to Edit participant details.
        Assert.Contains("Edit participant details", body);
        // Copy-to-invoice checkbox (AC5) and its script are present.
        Assert.Contains("id=\"CopyDetails\"", body);
        Assert.Contains("copy-customer-details-to-invoice.js", body);
        // Cancel (AC8) leaves the page for Home, like legacy's LeavePage().
        Assert.Matches("<a[^>]*href=\"/(Home(/Index)?)?\"[^>]*>\\s*Cancel", body);
    }

    [Fact]
    public async Task Index_ViewerOnlyUser_IsForbidden()
    {
        var client = CreateClient();
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync($"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Viewer")}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Customer/Index", cookies);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Index_OutstandingPendingUpdate_RendersReadOnlyNotice()
    {
        var client = CreateClient(details: SampleDetails with { HasOutstandingUpdate = true });
        var cookies = await SignInAsParticipantAsync(client, Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Customer/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Changes have already been submitted", body);
        Assert.Contains("disabled", body);
    }

    [Fact]
    public async Task Index_Post_ValidSubmission_RedirectsAndShowsConfirmationMessage()
    {
        var client = CreateClient(
            submitResult: new CustomerDetailsEditSaveResult(true, new Dictionary<string, string[]>()),
            allowAutoRedirect: false);
        var cookies = await SignInAsParticipantAsync(client, Guid.NewGuid());

        var getResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Customer/Index", cookies);
        CookieForwardingHttpClient.CaptureCookies(getResponse, cookies);
        var token = AntiforgeryTokenRegex().Match(await getResponse.Content.ReadAsStringAsync()).Groups[1].Value;

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContactName"] = "Jane Doe",
            ["Organisation"] = "Test Lab",
            ["Address1"] = "1 Test Street",
            ["Address2"] = "Testville",
            ["CountryId"] = Guid.NewGuid().ToString(),
            ["Telephone"] = "01234 567890",
            ["Email"] = "jane@example.com",
            ["InvoiceOrganisation"] = "Test Lab Invoicing",
            ["InvoiceAddress1"] = "1 Test Street",
            ["InvoiceAddress2"] = "Testville",
            ["InvoiceCountryId"] = Guid.NewGuid().ToString()
        };

        var postResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, "/Customer/Index", cookies, new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        CookieForwardingHttpClient.CaptureCookies(postResponse, cookies);

        var followUp = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Get, postResponse.Headers.Location!.ToString(), cookies);
        var followUpBody = await followUp.Content.ReadAsStringAsync();
        Assert.Contains("submitted for approval", followUpBody);
    }

    [Fact]
    public async Task Index_Post_MissingMandatoryFields_RedisplaysFormWithValidationMessages()
    {
        var client = CreateClient(submitResult: new CustomerDetailsEditSaveResult(false, new Dictionary<string, string[]>
        {
            ["ContactName"] = ["Enter Contact Name"],
            ["Organisation"] = ["Enter Organisation Name"]
        }));
        var cookies = await SignInAsParticipantAsync(client, Guid.NewGuid());

        var getResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Customer/Index", cookies);
        CookieForwardingHttpClient.CaptureCookies(getResponse, cookies);
        var token = AntiforgeryTokenRegex().Match(await getResponse.Content.ReadAsStringAsync()).Groups[1].Value;

        // ContactName/Organisation posted as empty strings (not omitted) - matches what a browser
        // actually sends when a participant clears a text input, which is what previously also
        // triggered ASP.NET Core's own implicit "The X field is required" alongside our message.
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContactName"] = "",
            ["Organisation"] = ""
        };

        var postResponse = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, "/Customer/Index", cookies, new FormUrlEncodedContent(form));
        var body = await postResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Enter Contact Name", body);
        Assert.Contains("Enter Organisation Name", body);
        // Guards against ASP.NET Core's implicit non-nullable-reference-type [Required] check
        // duplicating the Core-layer validator's own message for the same field.
        Assert.DoesNotContain("field is required", body);
        // Remains on the Edit Customer/Invoice Details page (not redirected away).
        Assert.Contains("Edit customer/invoice details", body);
    }

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
