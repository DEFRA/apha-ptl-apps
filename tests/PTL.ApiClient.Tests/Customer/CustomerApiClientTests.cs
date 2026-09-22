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

    [Fact]
    public async Task GetCustomersAsync_EmptyList_ReturnsEmptyList()
    {
        const string json = """[]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetCustomersAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCustomersAsync_NullResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetCustomersAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCustomerAsync_Found_ReturnsDeserializedResponse()
    {
        const string json = """{"customerId":"11111111-1111-1111-1111-111111111111","qalNumber":"QAL/00001","name":"Sample Labs","organisation":"Sample Labs","isActive":true}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetCustomerAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("Sample Labs", result.Name);
    }

    [Fact]
    public async Task SearchCustomersAsync_NullResponse_ReturnsDefaultSearchResponse()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.SearchCustomersAsync(new CustomerSearchRequest("test", CustomerStatusFilter.Active, 2, 50));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task CreateCustomerAsync_Success_ReturnsSuccessResultWithCustomer()
    {
        const string json = """{"customerId":"11111111-1111-1111-1111-111111111111","qalNumber":"QAL/00001","name":"Sample Labs","organisation":"Sample Labs","isActive":true}""";
        var client = CreateClient(HttpStatusCode.Created, json);

        var result = await client.CreateCustomerAsync(MinimalCreateRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Customer);
        Assert.Equal("Sample Labs", result.Customer.Name);
        Assert.Empty(result.FieldErrors);
    }

    [Fact]
    public async Task CreateCustomerAsync_BadRequest_NoErrorsInProblemDetails_ReturnsDefaultError()
    {
        const string json = """{"errors":{}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateCustomerAsync(MinimalCreateRequest());

        Assert.False(result.Success);
        Assert.Null(result.Customer);
        Assert.True(result.FieldErrors.ContainsKey(string.Empty));
        Assert.Equal("The request was invalid.", result.FieldErrors[string.Empty][0]);
    }

    [Fact]
    public async Task UpdateCustomerAsync_Success_ReturnsSuccessResultWithCustomer()
    {
        const string json = """{"customerId":"11111111-1111-1111-1111-111111111111","qalNumber":"QAL/00001","name":"Sample Labs","organisation":"Sample Labs","isActive":true}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.UpdateCustomerAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Customer);
        Assert.Equal("Sample Labs", result.Customer.Name);
    }

    [Fact]
    public async Task UpdateCustomerAsync_BadRequest_ReturnsFailureResult()
    {
        const string json = """{"errors":{"Name":["Enter a name."]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.UpdateCustomerAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.False(result.Success);
        Assert.Null(result.Customer);
        Assert.True(result.FieldErrors.ContainsKey("Name"));
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
