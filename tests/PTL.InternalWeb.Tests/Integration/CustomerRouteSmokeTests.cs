using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Customer;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the Customer Razor views (Index/Details/_CustomerForm)
// actually render at least once, rather than only being exercised via controller unit tests that
// never invoke the view engine.
public partial class CustomerRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeCustomerApiClient _fakeApiClient;

    public CustomerRouteSmokeTests(WebApplicationFactory<Program> factory)
    {
        var customerId = Guid.NewGuid();
        var customer = SampleCustomer(customerId);

        _fakeApiClient = new FakeCustomerApiClient
        {
            CustomerResponse = customer,
            SearchResponse = new CustomerSearchResponse([new CustomerSummaryResponse(customerId, customer.QalNumber, customer.Name, customer.Organisation, true)], 1, 1, 20)
        };

        CustomerId = customerId;
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICustomerApiClient>();
                services.AddSingleton<ICustomerApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
            }));
    }

    private Guid CustomerId { get; }

    private static CustomerResponse SampleCustomer(Guid customerId, bool isActive = true) => new(
        customerId, "QAL/00001", string.Empty, "Sample Laboratories Ltd", string.Empty, Guid.NewGuid(), string.Empty,
        Guid.NewGuid(), string.Empty, string.Empty, "Alice Example", "Sample Laboratories Ltd", "1 Sample Street",
        "Sample District", string.Empty, string.Empty, string.Empty, Guid.NewGuid(), "01234 567890", string.Empty,
        string.Empty, "alice@example.com", Guid.NewGuid(), string.Empty, DateTime.UtcNow, string.Empty, false,
        string.Empty, "Sample Laboratories Ltd", "1 Sample Street", "Sample District", string.Empty, string.Empty,
        string.Empty, Guid.NewGuid(), string.Empty, string.Empty, string.Empty, string.Empty, isActive, false,
        isActive ? null : DateTime.UtcNow, isActive ? null : Guid.NewGuid());

    private static PendingCustomerUpdateResponse SamplePendingUpdate(Guid customerId) => new(
        customerId, "New Contact", "New Organisation", "New Address 1", string.Empty, string.Empty, string.Empty, string.Empty,
        Guid.NewGuid(), string.Empty, string.Empty, string.Empty, "new@example.com", string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, Guid.NewGuid(), string.Empty, string.Empty, string.Empty, string.Empty);

    [Theory]
    [InlineData("/Customer/Index")]
    [InlineData("/Customer/Create")]
    public async Task StaticRoutes_ReturnSuccess(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Customer/Details/{CustomerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_RendersFullBreadcrumbTrail()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Customer/Details/{CustomerId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Manage Contracts", body);
        Assert.Contains("Customer Details", body);
    }

    [Fact]
    public async Task Details_InactiveCustomer_RendersInactiveDateRow()
    {
        _fakeApiClient.CustomerResponse = SampleCustomer(CustomerId, isActive: false);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Customer/Details/{CustomerId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Inactive date", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Edit_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Customer/Edit/{CustomerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Edit_RendersFullBreadcrumbTrail()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Customer/Edit/{CustomerId}");
        var crumbs = BreadcrumbHtml.Crumbs(await response.Content.ReadAsStringAsync());

        Assert.Equal(["Home", "Manage Contracts", "Customers", "Customer Details", "Edit Customer"], crumbs.Select(c => c.Text));
        Assert.Equal($"/Customer/Details/{CustomerId}", crumbs.Single(c => c.Text == "Customer Details").Href);
        Assert.Null(crumbs[^1].Href);
    }

    [Fact]
    public async Task EditPendingCustomerUpdate_RendersFullBreadcrumbTrail()
    {
        _fakeApiClient.PendingCustomerUpdateComparison = new PendingCustomerUpdateComparisonResponse(SampleCustomer(CustomerId), SamplePendingUpdate(CustomerId));
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Customer/EditPendingCustomerUpdate?customerId={CustomerId}");
        var crumbs = BreadcrumbHtml.Crumbs(await response.Content.ReadAsStringAsync());

        Assert.Equal(["Home", "Manage Contracts", "Customers", "Review Pending Customer Updates", "Pending Customer Update Details", "Edit Pending Customer Update"], crumbs.Select(c => c.Text));
        Assert.Equal($"/Customer/PendingCustomerUpdateDetails?customerId={CustomerId}", crumbs.Single(c => c.Text == "Pending Customer Update Details").Href);
        Assert.Null(crumbs[^1].Href);
    }

    [Fact]
    public async Task Index_NoResults_RendersEmptyState()
    {
        _fakeApiClient.SearchResponse = new CustomerSearchResponse([], 0, 1, 20);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Customer/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("There are no customers to display.", body);
        Assert.DoesNotContain("searchTerm", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Index_MultiplePages_RendersPagination()
    {
        var items = Enumerable.Range(1, 3)
            .Select(i => new CustomerSummaryResponse(Guid.NewGuid(), $"QAL/{i:00000}", $"Customer {i}", $"Org {i}", true))
            .ToList();
        _fakeApiClient.SearchResponse = new CustomerSearchResponse(items, 21, 1, 10);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Customer/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-pagination", body);
    }

    [Fact]
    public async Task Create_Post_InvalidModel_RendersFieldErrors()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/Customer/Create");

        var request = new HttpRequestMessage(HttpMethod.Post, "/Customer/Create")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["IsActive"] = "true",
                ["RegisteredFileNumber"] = "NOT-VALID",
                ["Telephone"] = "call-me",
                ["Fax"] = "call-me",
                ["Telephone2"] = "call-me",
                ["Email"] = "not-an-email",
                ["InvoiceTelephone"] = "call-me",
                ["InvoiceTelephone2"] = "call-me",
                ["InvoiceFax"] = "call-me",
                ["InvoiceEmail"] = "not-an-email",
                ["Name"] = new string('a', 60),
                ["Comments"] = new string('a', 2001)
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-form-group--error", body);
        Assert.Contains("govuk-error-summary", body);
        // Comments is rendered via govuk-textarea (not govuk-input/govuk-select) - assert its
        // own error path renders too, since it's the only field exercising that tag helper.
        Assert.Contains("Comments must not exceed 2000 characters", body);
    }

    [Fact]
    public async Task HomeError_RendersErrorView()
    {
        // HomeController.Error() is reachable directly regardless of environment, so this
        // exercises Views/Shared/Error.cshtml without needing to force a real unhandled exception
        // through the Production-only UseExceptionHandler pipeline.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Home/Error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("An error occurred while processing your request.", body);
    }

    [Fact]
    public async Task Edit_Post_InvalidModel_RendersFieldErrors()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, $"/Customer/Edit/{CustomerId}");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/Customer/Edit/{CustomerId}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["IsActive"] = "true"
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-form-group--error", body);
    }

    // Only the Details crumb is asserted: the re-render does not republish every ancestor id, so other crumbs may already be plain text.
    [Fact]
    public async Task Edit_Post_InvalidModel_KeepsTheDetailsBreadcrumbLinked()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, $"/Customer/Edit/{CustomerId}");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/Customer/Edit/{CustomerId}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["IsActive"] = "true"
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-form-group--error", body);
        var crumbs = BreadcrumbHtml.Crumbs(body);
        Assert.Equal($"/Customer/Details/{CustomerId}", crumbs.Single(c => c.Text == "Customer Details").Href);
        Assert.Equal("Edit Customer", crumbs[^1].Text);
        Assert.Null(crumbs[^1].Href);
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
