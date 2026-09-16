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
    public async Task DeactivateCustomerAsync_NotFound_ReturnsFailureResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.DeactivateCustomerAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ReactivateCustomerAsync_Success_ReturnsCustomer()
    {
        const string json = """{"customerId":"11111111-1111-1111-1111-111111111111","qalNumber":"QAL/00001","registeredFileNumber":"","name":"Sample Labs","previousName":"","customerTypeId":"22222222-2222-2222-2222-222222222222","vatNumber":"","vatRatingId":"00000000-0000-0000-0000-000000000000","accountNumber":"","customerFinanceId":"","contactName":"","organisation":"","address1":"","address2":"","address3":"","address4":"","address5":"","countryId":"00000000-0000-0000-0000-000000000000","telephone":"","telephone2":"","fax":"","email":"","currencyId":"00000000-0000-0000-0000-000000000000","comments":"","initialStartDate":"2026-01-01T00:00:00","postageArrangements":"","paymentNonUK":false,"invoiceName":"","invoiceOrganisation":"","invoiceAddress1":"","invoiceAddress2":"","invoiceAddress3":"","invoiceAddress4":"","invoiceAddress5":"","invoiceCountryId":"00000000-0000-0000-0000-000000000000","invoiceTelephone":"","invoiceTelephone2":"","invoiceFax":"","invoiceEmail":"","isActive":true,"canOrderOnline":false,"inactiveDate":null,"customerStatusId":null}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.ReactivateCustomerAsync(Guid.NewGuid());

        Assert.True(result.Success);
        Assert.True(result.Customer!.IsActive);
    }

    private static CreateCustomerRequest MinimalCreateRequest() => new(
        string.Empty, string.Empty, string.Empty, Guid.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty, false,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty);

    private static UpdateCustomerRequest MinimalUpdateRequest() => new(
        string.Empty, string.Empty, string.Empty, Guid.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty, false,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, true, false, null);
}
