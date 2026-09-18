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
public class CustomerRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
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
    public async Task Details_InactiveCustomer_RendersInactiveDateRow()
    {
        _fakeApiClient.CustomerResponse = SampleCustomer(CustomerId, isActive: false);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Customer/Details/{CustomerId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Inactive date", body);
    }

    [Fact]
    public async Task Edit_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Customer/Edit/{CustomerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_NoResults_RendersEmptyState()
    {
        _fakeApiClient.SearchResponse = new CustomerSearchResponse([], 0, 1, 20);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Customer/Index?searchTerm=nothing-matches");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("There are no customers to display.", body);
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
                ["Name"] = new string('a', 60)
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-form-group--error", body);
        Assert.Contains("govuk-error-summary", body);
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

    private static async Task<(string Token, string Cookie)> GetAntiforgeryAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        var token = Regex.Match(body, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var cookie = string.Join("; ", response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(c => c.Split(';')[0])
            : []);
        return (token, cookie);
    }
}
