using System.Net;
using PTL.Contracts.CreateOrder;

namespace PTL.ApiClient.Tests.CreateOrder;

public class CreateOrderApiClientTests
{
    private const string OrderJson = """
        {"year":"2026/27","currencySymbol":"£","qalNumber":"QAL/1","labCode":"LAB001","isAphaCustomer":false,
         "participatingSchemes":[],"availableSchemes":[],"schemeTotal":10.0,"postageTotal":5.0,"orderTotal":15.0,"canProceed":true}
        """;

    private sealed class CapturedRequest
    {
        public HttpMethod? Method { get; set; }

        public string? Path { get; set; }

        public string? Query { get; set; }
    }

    private static (CreateOrderApiClient Client, CapturedRequest Request) CreateClient(HttpStatusCode statusCode, string? jsonContent)
    {
        var captured = new CapturedRequest();
        var handler = new StubHttpMessageHandler(statusCode, jsonContent)
        {
            OnRequest = request =>
            {
                captured.Method = request.Method;
                captured.Path = request.RequestUri!.AbsolutePath;
                captured.Query = request.RequestUri.Query;
            }
        };
        return (new CreateOrderApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), captured);
    }

    [Fact]
    public async Task GetEligibilityAsync_NotFound_ReturnsNull()
    {
        var (client, _) = CreateClient(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetEligibilityAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetEligibilityAsync_Success_ReturnsFlags()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, """{"canOrderOnline":true,"canCreateCurrentYearOrder":false,"canCreateNextYearOrder":true}""");

        var result = await client.GetEligibilityAsync(Guid.NewGuid());

        Assert.Equal(new CreateOrderEligibilityResponse(true, false, true), result);
    }

    [Fact]
    public async Task GetAsync_Success_ReturnsOrder()
    {
        var (client, request) = CreateClient(HttpStatusCode.OK, OrderJson);
        var participantId = Guid.NewGuid();

        var result = await client.GetAsync(participantId, "current");

        Assert.Equal(CreateOrderApiStatus.Ok, result.Status);
        Assert.Equal("2026/27", result.Order!.Year);
        Assert.True(result.Order.CanProceed);
        Assert.Equal($"/api/create-order/{participantId}/current", request.Path);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, CreateOrderApiStatus.NotAllowed)]
    [InlineData(HttpStatusCode.NotFound, CreateOrderApiStatus.NotFound)]
    public async Task GetAsync_ForbiddenOrNotFound_MapsToStatus(HttpStatusCode statusCode, CreateOrderApiStatus expected)
    {
        var (client, _) = CreateClient(statusCode, null);

        var result = await client.GetAsync(Guid.NewGuid(), "next");

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Order);
    }

    [Fact]
    public async Task GetAsync_ServerError_Throws()
    {
        var (client, _) = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(Guid.NewGuid(), "current"));
    }

    [Fact]
    public async Task ChangeAsync_Success_PostsToChangeRouteAndReturnsOrder()
    {
        var (client, request) = CreateClient(HttpStatusCode.OK, OrderJson);
        var participantId = Guid.NewGuid();

        var result = await client.ChangeAsync(participantId, "next", new CreateOrderLineChangeRequest(Guid.NewGuid(), "select", true));

        Assert.Equal(CreateOrderApiStatus.Ok, result.Status);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/create-order/{participantId}/next/change", request.Path);
    }

    [Fact]
    public async Task ClearAsync_NoContent_ReturnsOk()
    {
        var (client, request) = CreateClient(HttpStatusCode.NoContent, null);
        var participantId = Guid.NewGuid();

        var status = await client.ClearAsync(participantId, "current");

        Assert.Equal(CreateOrderApiStatus.Ok, status);
        Assert.Equal($"/api/create-order/{participantId}/current/clear", request.Path);
    }

    [Fact]
    public async Task ClearAsync_Forbidden_ReturnsNotAllowed()
    {
        var (client, _) = CreateClient(HttpStatusCode.Forbidden, null);

        Assert.Equal(CreateOrderApiStatus.NotAllowed, await client.ClearAsync(Guid.NewGuid(), "current"));
    }

    [Fact]
    public async Task GetAsync_ForReview_AsksForTheReviewDetails()
    {
        var json = OrderJson.Replace("\"canProceed\":true", "\"canProceed\":true,\"purchaseOrderNumber\":\"PO-1\",\"consents\":[{\"schemeId\":\"11111111-1111-1111-1111-111111111111\",\"identifier\":\"PT1\",\"name\":\"Alpha\",\"text\":\"Share\",\"given\":false}]", StringComparison.Ordinal);
        var (client, request) = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetAsync(Guid.NewGuid(), "current", review: true);

        Assert.Equal("?review=true", request.Query);
        Assert.Equal("PO-1", result.Order!.PurchaseOrderNumber);
        Assert.Equal("Share", Assert.Single(result.Order.DataConsents).Text);
    }

    [Fact]
    public async Task GetAsync_WithoutReview_HasNoQueryAndNoConsents()
    {
        var (client, request) = CreateClient(HttpStatusCode.OK, OrderJson);

        var result = await client.GetAsync(Guid.NewGuid(), "current");

        Assert.Equal(string.Empty, request.Query);
        Assert.Equal(string.Empty, result.Order!.PurchaseOrderNumber);
        Assert.Empty(result.Order.DataConsents);
    }

    [Fact]
    public async Task SavePurchaseOrderAsync_NoContent_PostsToThePurchaseOrderRoute()
    {
        var (client, request) = CreateClient(HttpStatusCode.NoContent, null);
        var participantId = Guid.NewGuid();

        var result = await client.SavePurchaseOrderAsync(participantId, "next", new CreateOrderSaveRequest("PO-1"));

        Assert.True(result.Succeeded);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/create-order/{participantId}/next/purchase-order", request.Path);
    }

    [Fact]
    public async Task SubmitAsync_NoContent_PostsToTheSubmitRoute()
    {
        var (client, request) = CreateClient(HttpStatusCode.NoContent, null);
        var participantId = Guid.NewGuid();

        var result = await client.SubmitAsync(participantId, "current", new CreateOrderSubmitRequest("PO-1", true, []));

        Assert.True(result.Succeeded);
        Assert.Equal($"/api/create-order/{participantId}/current/submit", request.Path);
    }

    [Fact]
    public async Task SubmitAsync_BadRequest_ReturnsTheFieldErrors()
    {
        var (client, _) = CreateClient(HttpStatusCode.BadRequest, """{"errors":[{"field":"TermsAccepted","message":"Accept the terms"}]}""");

        var result = await client.SubmitAsync(Guid.NewGuid(), "current", new CreateOrderSubmitRequest(null, false, null));

        Assert.False(result.Succeeded);
        Assert.Equal(CreateOrderApiStatus.Ok, result.Status);
        Assert.Equal(new CreateOrderValidationErrorResponse("TermsAccepted", "Accept the terms"), Assert.Single(result.Errors));
    }

    [Fact]
    public async Task SubmitAsync_BadRequestWithoutABody_ReturnsNoErrors()
    {
        var (client, _) = CreateClient(HttpStatusCode.BadRequest, "null");

        var result = await client.SubmitAsync(Guid.NewGuid(), "current", new CreateOrderSubmitRequest(null, false, null));

        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, CreateOrderApiStatus.NotAllowed)]
    [InlineData(HttpStatusCode.NotFound, CreateOrderApiStatus.NotFound)]
    public async Task SaveAndSubmit_ForbiddenOrNotFound_MapToStatus(HttpStatusCode statusCode, CreateOrderApiStatus expected)
    {
        var (client, _) = CreateClient(statusCode, null);

        Assert.Equal(expected, (await client.SavePurchaseOrderAsync(Guid.NewGuid(), "current", new CreateOrderSaveRequest("PO"))).Status);
        Assert.Equal(expected, (await client.SubmitAsync(Guid.NewGuid(), "current", new CreateOrderSubmitRequest("PO", true, []))).Status);
    }

    [Fact]
    public async Task SubmitAsync_ServerError_Throws()
    {
        var (client, _) = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SubmitAsync(Guid.NewGuid(), "current", new CreateOrderSubmitRequest("PO", true, [])));
    }
}
