using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.Contract;

namespace PTL.ApiClient.Tests.Contract;

public class ContractApiClientTests
{
    private static ContractApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetContractAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetContractAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetContractAsync_Success_ReturnsDeserializedContract()
    {
        var client = CreateClient(HttpStatusCode.OK, FullContractJson());

        var result = await client.GetContractAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("QAL0001", result!.QalNumber);
        Assert.True(result.IsActive);
        Assert.False(result.IsReadOnly);
    }

    [Fact]
    public async Task GetContractsForCustomerAsync_WithYearId_ReturnsDeserializedSearchResponse()
    {
        const string json = """{"items":[],"totalCount":0,"page":1,"pageSize":20}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetContractsForCustomerAsync(Guid.NewGuid(), new ContractSearchRequest(YearId: 2026));

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetContractsForCustomerByYearAsync_ReturnsDeserializedSearchResponse()
    {
        const string json = """{"items":[],"totalCount":0,"page":1,"pageSize":20}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetContractsForCustomerByYearAsync(Guid.NewGuid(), 2026);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task CreateContractAsync_Success_ReturnsSavedContract()
    {
        var client = CreateClient(HttpStatusCode.OK, FullContractJson());

        var result = await client.CreateContractAsync(Guid.NewGuid(), MinimalCreateRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Contract);
        Assert.Empty(result.FieldErrors);
    }

    [Fact]
    public async Task CreateContractAsync_BadRequestWithNoErrors_ReturnsGenericFieldError()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, "{}");

        var result = await client.CreateContractAsync(Guid.NewGuid(), MinimalCreateRequest());

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey(string.Empty));
    }

    [Fact]
    public async Task CreateContractAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string json = """{"errors":{"UTNumber":["Enter either a UT number or an FT number, but not both."]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateContractAsync(Guid.NewGuid(), MinimalCreateRequest());

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey("UTNumber"));
    }

    [Fact]
    public async Task UpdateContractAsync_NotFound_ReturnsFailureResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateContractAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.False(result.Success);
        Assert.Null(result.Contract);
    }

    [Fact]
    public async Task UpdateContractAsync_Success_ReturnsSavedContract()
    {
        var client = CreateClient(HttpStatusCode.OK, FullContractJson());

        var result = await client.UpdateContractAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Contract);
    }

    private static string FullContractJson() => """
        {
            "contractId":"11111111-1111-1111-1111-111111111111",
            "customerId":"22222222-2222-2222-2222-222222222222",
            "customerName":"Test Customer",
            "qalNumber":"QAL0001",
            "yearId":2026,
            "utNumber":"UT1",
            "ftNumber":"",
            "contractSignatory":"A Signatory",
            "actionsRequired":"",
            "renewalInformation":"",
            "discountRate":0,
            "administrationCharge":0,
            "numberCourier":0,
            "courierPrice":0,
            "numberPostage":0,
            "postagePrice":0,
            "numberSpecialDelivery":0,
            "specialDeliveryPrice":0,
            "acknowledgementPostedDate":null,
            "acknowledgementReturnedDate":null,
            "jobSheetPostedDate":null,
            "reasonForClosure":"",
            "dateOfLeaving":null,
            "isActive":true,
            "isReadOnly":false,
            "suffix":"A",
            "commencementDate":null,
            "purchaseOrderNumber":"",
            "optOutOfInvoiceGeneration":false,
            "isInvoiceSent":false,
            "isOnlineOrder":false,
            "approvedBy":null,
            "approvedDate":null
        }
        """;

    private static ContractRequest MinimalCreateRequest() => new(
        2027, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, 0, 0, 0, 0,
        DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, string.Empty, DateTime.UtcNow, true, string.Empty, string.Empty, false, false);

    private static ContractRequest MinimalUpdateRequest() => MinimalCreateRequest();
}
