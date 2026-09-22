using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Contract;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

/// <summary>
/// Enhanced Contract route tests covering additional page rendering scenarios.
/// Ensures all Contract Razor view code paths are exercised for coverage.
/// </summary>
public class ContractRouteSmokeEdgeCaseTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeContractApiClient _fakeApiClient;
    private readonly Guid _customerId;
    private readonly Guid _contractId;

    public ContractRouteSmokeEdgeCaseTests(WebApplicationFactory<Program> factory)
    {
        _customerId = Guid.NewGuid();
        _contractId = Guid.NewGuid();

        _fakeApiClient = new FakeContractApiClient
        {
            ContractResponse = SampleContract(_contractId, _customerId),
            SearchResponse = new ContractSearchResponse([], 0, 1, 20)
        };

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IContractApiClient>();
                services.AddSingleton<IContractApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
            }));
    }

    private static ContractResponse SampleContract(Guid contractId, Guid customerId, bool isInactive = false)
    {
        var inactiveDate = isInactive ? DateTime.UtcNow : (DateTime?)null;
        var approvedDate = isInactive ? DateTime.UtcNow.AddDays(-30) : (DateTime?)null;

        return new(contractId, customerId, "Sample Laboratories Ltd", "QAL/00001",
            DateTime.UtcNow.Year + 1, "UT12345", string.Empty, "Alice Example", string.Empty, string.Empty,
            0, 0, 0, 0, 0, 0, 0, 0, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, string.Empty,
            DateTime.UtcNow, true, false, "A", DateTime.UtcNow, string.Empty,
            false, false, false, null,
            approvedDate);
    }

    [Fact]
    public async Task Details_WithValidContractId_RendersSuccessfully()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Details/{_contractId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_WithInactiveContract_RendersSuccessfully()
    {
        _fakeApiClient.ContractResponse = SampleContract(_contractId, _customerId, isInactive: true);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Details/{_contractId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_WithEmptyResults_RendersSuccessfully()
    {
        _fakeApiClient.SearchResponse = new ContractSearchResponse([], 0, 1, 20);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Index?customerId={_customerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_WithMultipleResults_RendersSuccessfully()
    {
        var items = Enumerable.Range(1, 3)
            .Select(i => new ContractSummaryResponse(Guid.NewGuid(), _customerId, DateTime.UtcNow.Year + 1, true, $"UT{i:00000}"))
            .ToList();
        _fakeApiClient.SearchResponse = new ContractSearchResponse(items, 21, 1, 10);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Index?customerId={_customerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_WithSearchTerm_RendersSuccessfully()
    {
        var items = new[] { new ContractSummaryResponse(_contractId, _customerId, DateTime.UtcNow.Year + 1, true, "UT12345") };
        _fakeApiClient.SearchResponse = new ContractSearchResponse(items, 1, 1, 20);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Index?customerId={_customerId}&searchTerm=UT12345");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_Get_RendersFormSuccessfully()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Create?customerId={_customerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Edit_Get_RendersFormSuccessfully()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Contract/Edit/{_contractId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Edit_Post_WithValidData_SubmitsFormSuccessfully()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, $"/Contract/Edit/{_contractId}");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/Contract/Edit/{_contractId}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["CustomerName"] = "Sample Laboratories Ltd",
                ["QalNumber"] = "QAL/00001",
                ["UTNumber"] = "UT12345",
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        // Expect either OK (successful submission) or a redirect
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Found, $"Unexpected status: {response.StatusCode}");
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
