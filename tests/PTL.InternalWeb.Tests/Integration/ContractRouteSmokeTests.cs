using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Contract;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the Contract Razor views (Index/Details/Create/Edit/_ContractForm)
// actually render at least once, rather than only being exercised via controller unit tests that
// never invoke the view engine.
public class ContractRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeContractApiClient _fakeApiClient;

    public ContractRouteSmokeTests(WebApplicationFactory<Program> factory)
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var contract = SampleContract(contractId, customerId);

        _fakeApiClient = new FakeContractApiClient
        {
            ContractResponse = contract,
            SearchResponse = new ContractSearchResponse([new ContractSummaryResponse(contractId, customerId, contract.YearId, true, contract.Suffix)], 1, 1, 20)
        };

        CustomerId = customerId;
        ContractId = contractId;
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IContractApiClient>();
                services.AddSingleton<IContractApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
            }));
    }

    private Guid CustomerId { get; }

    private Guid ContractId { get; }

    private static ContractResponse SampleContract(Guid contractId, Guid customerId) => new(
        contractId, customerId, "Sample Laboratories Ltd", "QAL/00001", DateTime.UtcNow.Year + 1, "UT12345",
        string.Empty, "Alice Example", string.Empty, string.Empty, 0, 0, 0, 0, 0, 0, 0, 0,
        DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, string.Empty, DateTime.UtcNow, true, false, "A",
        DateTime.UtcNow, string.Empty, false, false, false, null, null);

    [Fact]
    public async Task Index_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Index?customerId={CustomerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Details/{ContractId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_RendersFullBreadcrumbTrail()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Details/{ContractId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Manage Contracts", body);
        Assert.Contains("Customers", body);
        Assert.Contains("Contracts", body);
        Assert.Contains("Contract Details", body);
    }

    [Fact]
    public async Task Create_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Create?customerId={CustomerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Edit_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Edit/{ContractId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Edit_RendersFullBreadcrumbTrail()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Edit/{ContractId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Manage Contracts", body);
        Assert.Contains("Customers", body);
        Assert.Contains("Contracts", body);
        Assert.Contains("Edit Contract", body);
    }

    [Fact]
    public async Task Index_NoResults_RendersEmptyState()
    {
        _fakeApiClient.SearchResponse = new ContractSearchResponse([], 0, 1, 20);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Index?customerId={CustomerId}&searchTerm=nothing-matches");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("There are no contracts to display.", body);
    }
}
