using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Contract;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// The Contracts list must reopen with the user's last search when they come back to it from a
// contract, the breadcrumb or a Cancel button - all of which link with only the customer id.
public class ContractIndexSearchStateTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid CustomerId = Guid.Parse("99999999-0000-0000-0000-0000000000a1");
    private static readonly Guid OtherCustomerId = Guid.Parse("99999999-0000-0000-0000-0000000000a2");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeContractApiClient _contracts = new();

    public ContractIndexSearchStateTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IContractApiClient>();
                services.AddSingleton<IContractApiClient>(_contracts);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
                services.RemoveAll<ICustomerApiClient>();
                services.AddSingleton<ICustomerApiClient>(new FakeCustomerApiClient());
            }));

    private HttpClient CreateHttpsClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private async Task<ContractSearchRequest> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return _contracts.LastSearchRequest!;
    }

    [Fact]
    public async Task ReturningWithOnlyTheCustomer_RestoresTheLastSearch()
    {
        var client = CreateHttpsClient();
        await GetAsync(client, $"/Contract/Index?customerId={CustomerId}&period=Historical&page=2&pageSize=50");

        var restored = await GetAsync(client, $"/Contract/Index?customerId={CustomerId}");

        Assert.Equal(ContractPeriodFilter.Historical, restored.Period);
        Assert.Equal(2, restored.Page);
        Assert.Equal(50, restored.PageSize);
    }

    [Fact]
    public async Task ChoosingTheDefaultPeriodExplicitly_ReplacesTheSavedSearch()
    {
        var client = CreateHttpsClient();
        await GetAsync(client, $"/Contract/Index?customerId={CustomerId}&period=All");
        await GetAsync(client, $"/Contract/Index?customerId={CustomerId}&period=CurrentAndNext");

        var restored = await GetAsync(client, $"/Contract/Index?customerId={CustomerId}");

        Assert.Equal(ContractPeriodFilter.CurrentAndNext, restored.Period);
    }

    [Fact]
    public async Task ADifferentCustomer_GetsTheDefaults()
    {
        var client = CreateHttpsClient();
        await GetAsync(client, $"/Contract/Index?customerId={CustomerId}&period=All");

        var other = await GetAsync(client, $"/Contract/Index?customerId={OtherCustomerId}");

        Assert.Equal(ContractPeriodFilter.CurrentAndNext, other.Period);
    }

    [Fact]
    public async Task NoSavedSearch_UsesTheDefaults()
    {
        var request = await GetAsync(CreateHttpsClient(), $"/Contract/Index?customerId={CustomerId}");

        Assert.Equal(ContractPeriodFilter.CurrentAndNext, request.Period);
        Assert.Equal(1, request.Page);
    }

    [Fact]
    public async Task ATamperedCookie_IsIgnored()
    {
        var client = _factory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Get, $"/Contract/Index?customerId={CustomerId}");
        message.Headers.Add("Cookie", "ptl-contract-search=not-json");

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ContractPeriodFilter.CurrentAndNext, _contracts.LastSearchRequest!.Period);
    }
}
