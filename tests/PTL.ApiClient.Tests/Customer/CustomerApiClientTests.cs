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
    public async Task SearchCustomersAsync_WithNullSearchTerm_ReturnsDeserializedSearchResponse()
    {
        const string json = """{"items":[],"totalCount":0,"page":1,"pageSize":20}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SearchCustomersAsync(new CustomerSearchRequest());

        Assert.Empty(result.Items);
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
    public async Task CreateCustomerAsync_BadRequestWithNullBody_ReturnsDefaultError()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, "null");

        var result = await client.CreateCustomerAsync(MinimalCreateRequest());

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey(string.Empty));
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

    [Fact]
    public async Task GetPendingCustomerUpdatesAsync_ReturnsDeserializedList()
    {
        const string json = """[{"customerId":"11111111-1111-1111-1111-111111111111","qalNumber":"QAL/00001","name":"Sample Labs"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetPendingCustomerUpdatesAsync();

        Assert.Single(result);
        Assert.Equal("Sample Labs", result[0].Name);
    }

    [Fact]
    public async Task GetPendingCustomerUpdatesAsync_NullResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetPendingCustomerUpdatesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPendingCustomerUpdateAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetPendingCustomerUpdateAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPendingCustomerUpdateAsync_Found_ReturnsComparison()
    {
        const string json = """{"current":{"customerId":"11111111-1111-1111-1111-111111111111","qalNumber":"QAL/00001","name":"Sample Labs","organisation":"Sample Labs","isActive":true},"pending":{"customerId":"11111111-1111-1111-1111-111111111111","contactName":"New Contact","organisation":"","address1":"","address2":"","address3":"","address4":"","address5":"","countryId":"00000000-0000-0000-0000-000000000000","telephone":"","telephone2":"","fax":"","email":"","invoiceName":"","invoiceOrganisation":"","invoiceAddress1":"","invoiceAddress2":"","invoiceAddress3":"","invoiceAddress4":"","invoiceAddress5":"","invoiceCountryId":"00000000-0000-0000-0000-000000000000","invoiceTelephone":"","invoiceTelephone2":"","invoiceFax":"","invoiceEmail":""}}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetPendingCustomerUpdateAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("Sample Labs", result.Current.Name);
        Assert.Equal("New Contact", result.Pending.ContactName);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdateAsync_NotFound_ReturnsNotFoundResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.ApprovePendingCustomerUpdateAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.True(result.NotFound);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdateAsync_Success_ReturnsSuccessResult()
    {
        var client = CreateClient(HttpStatusCode.NoContent, null);

        var result = await client.ApprovePendingCustomerUpdateAsync(Guid.NewGuid());

        Assert.True(result.Success);
        Assert.False(result.NotFound);
        Assert.Empty(result.FieldErrors);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdateAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string json = """{"errors":{"ContactName":["Contact name is required."]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.ApprovePendingCustomerUpdateAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.False(result.NotFound);
        Assert.True(result.FieldErrors.ContainsKey("ContactName"));
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdateAsync_WithAmendments_ReturnsSuccessResult()
    {
        var client = CreateClient(HttpStatusCode.NoContent, null);

        var result = await client.ApprovePendingCustomerUpdateAsync(Guid.NewGuid(), AmendedRequest());

        Assert.True(result.Success);
        Assert.False(result.NotFound);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdateAsync_BadRequestWithNullBody_ReturnsGenericError()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, "null");

        var result = await client.ApprovePendingCustomerUpdateAsync(Guid.NewGuid(), AmendedRequest());

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey(string.Empty));
    }

    private static PendingCustomerUpdateSaveRequest AmendedRequest() => new(
        "New Contact", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
        Guid.NewGuid(), string.Empty, string.Empty, string.Empty, "new@example.com",
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
        Guid.NewGuid(), string.Empty, string.Empty, string.Empty, string.Empty);

    [Fact]
    public async Task ApprovePendingCustomerUpdateAsync_BadRequestWithNoErrors_ReturnsDefaultError()
    {
        const string json = """{"errors":{}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.ApprovePendingCustomerUpdateAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("The request was invalid.", result.FieldErrors[string.Empty][0]);
    }

    [Fact]
    public async Task DeclinePendingCustomerUpdateAsync_NotFound_ReturnsFalse()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.DeclinePendingCustomerUpdateAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task DeclinePendingCustomerUpdateAsync_Success_ReturnsTrue()
    {
        var client = CreateClient(HttpStatusCode.NoContent, null);

        var result = await client.DeclinePendingCustomerUpdateAsync(Guid.NewGuid());

        Assert.True(result);
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
