using System.Net;
using PTL.ApiClient;
using PTL.Contracts.Contract;

namespace PTL.ApiClient.Tests.Contract;

public class PendingOrderApiClientTests
{
    private static ContractApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetPendingOrdersAsync_ReturnsBothGrids()
    {
        const string json = """{"currentYearOrders":[{"pendingContractId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","qalNumber":"QAL0001","customerName":"Sample Labs","yearId":2026,"year":"2026/27","orderSubmitDate":"2026-01-01T00:00:00Z"}],"nextYearOrders":[]}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetPendingOrdersAsync();

        Assert.Single(result.CurrentYearOrders);
        Assert.Empty(result.NextYearOrders);
        Assert.Equal("2026/27", result.CurrentYearOrders[0].Year);
    }

    [Fact]
    public async Task GetPendingOrdersAsync_NullResponse_ReturnsEmptyGrids()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetPendingOrdersAsync();

        Assert.Empty(result.CurrentYearOrders);
        Assert.Empty(result.NextYearOrders);
    }

    [Fact]
    public async Task GetPendingOrderAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetPendingOrderAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPendingOrderAsync_Found_ReturnsPricedOrder()
    {
        const string json = """{"pendingContractId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","qalNumber":"QAL0001","customerName":"Sample Labs","yearId":2026,"year":"2026/27","purchaseOrderNumber":"PO-1","currencySymbol":"£","schemes":[],"totalSchemePrice":100.0,"totalPostagePrice":10.0,"total":110.0}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetPendingOrderAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal(110m, result!.Total);
        Assert.Equal("PO-1", result.PurchaseOrderNumber);
    }

    [Fact]
    public async Task UpdatePendingOrderSchemeAsync_Success_ReturnsTrue()
    {
        var client = CreateClient(HttpStatusCode.NoContent, null);

        Assert.True(await client.UpdatePendingOrderSchemeAsync(Guid.NewGuid(), Guid.NewGuid(), UpdateRequest()));
    }

    [Fact]
    public async Task UpdatePendingOrderSchemeAsync_NotFound_ReturnsFalse()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        Assert.False(await client.UpdatePendingOrderSchemeAsync(Guid.NewGuid(), Guid.NewGuid(), UpdateRequest()));
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_Success_ReturnsSuccessResult()
    {
        var client = CreateClient(HttpStatusCode.NoContent, null);

        var result = await client.ApprovePendingOrderAsync(Guid.NewGuid(), new PendingOrderApproveRequest("PO-2"));

        Assert.True(result.Success);
        Assert.False(result.NotFound);
        Assert.Empty(result.FieldErrors);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_NotFound_ReturnsNotFoundResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.ApprovePendingOrderAsync(Guid.NewGuid(), new PendingOrderApproveRequest("PO-2"));

        Assert.False(result.Success);
        Assert.True(result.NotFound);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string json = """{"errors":{"PurchaseOrderNumber":["Too long."]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.ApprovePendingOrderAsync(Guid.NewGuid(), new PendingOrderApproveRequest("PO-2"));

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey("PurchaseOrderNumber"));
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_BadRequestWithNoErrors_ReturnsDefaultError()
    {
        const string json = """{"errors":{}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.ApprovePendingOrderAsync(Guid.NewGuid(), new PendingOrderApproveRequest("PO-2"));

        Assert.Equal("The request was invalid.", result.FieldErrors[string.Empty][0]);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_BadRequestWithNullBody_ReturnsDefaultError()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, "null");

        var result = await client.ApprovePendingOrderAsync(Guid.NewGuid(), new PendingOrderApproveRequest("PO-2"));

        Assert.False(result.Success);
        Assert.Equal("The request was invalid.", result.FieldErrors[string.Empty][0]);
    }

    [Fact]
    public async Task DeclinePendingOrderAsync_Success_ReturnsTrue()
    {
        var client = CreateClient(HttpStatusCode.NoContent, null);

        Assert.True(await client.DeclinePendingOrderAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeclinePendingOrderAsync_NotFound_ReturnsFalse()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        Assert.False(await client.DeclinePendingOrderAsync(Guid.NewGuid()));
    }

    private static PendingOrderSchemeUpdateRequest UpdateRequest() =>
        new(false, false, false, true, false, false, false, false, false, false, false, false, true, false);
}
