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
}
