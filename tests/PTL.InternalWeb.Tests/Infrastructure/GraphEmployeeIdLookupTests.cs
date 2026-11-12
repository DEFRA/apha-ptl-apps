using System.Net;
using System.Text;
using PTL.Common.Graph;
using PTL.InternalWeb.Infrastructure;

namespace PTL.InternalWeb.Tests.Infrastructure;

public class GraphEmployeeIdLookupTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public string? LastName { get; private set; }

        public HttpClient CreateClient(string name)
        {
            LastName = name;
            return new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://graph.example.test/v1.0/") };
        }
    }

    private static StubHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    [Theory]
    [InlineData("""{"employeeId":"12345"}""", "12345")]
    [InlineData("""{"employeeId":"  12345 "}""", "12345")]
    [InlineData("""{"employeeId":" "}""", null)]
    [InlineData("""{"employeeId":null}""", null)]
    [InlineData("""{}""", null)]
    [InlineData("null", null)]
    public async Task FindEmployeeIdAsync_ReadsTheEmployeeIdOfTheUser(string json, string? expected)
    {
        var objectId = Guid.NewGuid();
        var handler = Json(json);
        var factory = new Factory(handler);

        var employeeId = await new GraphEmployeeIdLookup(factory).FindEmployeeIdAsync(objectId, CancellationToken.None);

        Assert.Equal(expected, employeeId);
        Assert.Equal("Graph", factory.LastName);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"https://graph.example.test/v1.0/users/{objectId:D}?$select=employeeId", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task FindEmployeeIdAsync_ReturnsNull_WhenTheUserDoesNotExist()
    {
        var lookup = new GraphEmployeeIdLookup(new Factory(Json("""{"error":{"code":"Request_ResourceNotFound"}}""", HttpStatusCode.NotFound)));

        Assert.Null(await lookup.FindEmployeeIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task FindEmployeeIdAsync_GraphFailure_ThrowsWithTheStatusOnly()
    {
        var lookup = new GraphEmployeeIdLookup(new Factory(Json("""{"error":{"message":"secret detail"}}""", HttpStatusCode.Forbidden)));

        var exception = await Assert.ThrowsAsync<GraphException>(() => lookup.FindEmployeeIdAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Contains("403", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret detail", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindEmployeeIdAsync_WrapsTransportFailures()
    {
        var lookup = new GraphEmployeeIdLookup(new Factory(new StubHandler(_ => throw new HttpRequestException("network"))));

        var exception = await Assert.ThrowsAsync<GraphException>(() => lookup.FindEmployeeIdAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task FindEmployeeIdAsync_WrapsUnreadableResponses()
    {
        var lookup = new GraphEmployeeIdLookup(new Factory(Json("not json")));

        await Assert.ThrowsAsync<GraphException>(() => lookup.FindEmployeeIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task FindEmployeeIdAsync_PassesAGraphExceptionThroughUnwrapped()
    {
        var failure = new GraphException("no token");
        var lookup = new GraphEmployeeIdLookup(new Factory(new StubHandler(_ => throw failure)));

        var thrown = await Assert.ThrowsAsync<GraphException>(() => lookup.FindEmployeeIdAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task FindEmployeeIdAsync_DoesNotWrapCancellationRequestedByTheCaller()
    {
        using var cancellation = new CancellationTokenSource();
        var lookup = new GraphEmployeeIdLookup(new Factory(new StubHandler(_ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        })));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup.FindEmployeeIdAsync(Guid.NewGuid(), cancellation.Token));
    }
}
