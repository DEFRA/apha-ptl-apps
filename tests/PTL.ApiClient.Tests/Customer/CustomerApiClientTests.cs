using System.Net;
using System.Net.Http.Json;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.Customer;

namespace PTL.ApiClient.Tests.Customer;

public class CustomerApiClientTests
{
    private static CustomerApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetCustomersAsync_ReturnsDeserializedList()
    {
        const string json = """[{"customerId":"11111111-1111-1111-1111-111111111111","qalNumber":"QAL/00001","name":"Sample Labs","organisation":"Sample Labs","isActive":true}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetCustomersAsync();

        Assert.Single(result);
        Assert.Equal("Sample Labs", result[0].Name);
    }

    [Fact]
    public async Task GetCustomerAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetCustomerAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SearchCustomersAsync_ReturnsDeserializedSearchResponse()
    {
        const string json = """{"items":[],"totalCount":0,"page":1,"pageSize":20}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SearchCustomersAsync(new CustomerSearchRequest("test", CustomerStatusFilter.Active, 1, 20));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task CreateCustomerAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string json = """{"errors":{"Name":["Enter a name."]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateCustomerAsync(MinimalCreateRequest());

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey("Name"));
    }

    [Fact]
    public async Task UpdateCustomerAsync_NotFound_ReturnsFailureResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateCustomerAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.False(result.Success);
        Assert.Null(result.Customer);
    }

    private static CustomerSaveRequest MinimalCreateRequest() => new(
        string.Empty, string.Empty, string.Empty, Guid.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty, false,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty);

    private static CustomerSaveRequest MinimalUpdateRequest() => new(
        string.Empty, string.Empty, string.Empty, Guid.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty, false,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, true, false, null);
}
