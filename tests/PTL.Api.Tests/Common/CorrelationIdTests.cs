using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PTL.Common.Correlation;

namespace PTL.Api.Tests.Common;

public class CorrelationIdTests
{
    [Fact]
    public async Task DelegatingHandler_WithCorrelationIdOnContext_ForwardsHeader()
    {
        var correlationId = Guid.NewGuid().ToString();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddlewareExtensions.HeaderName] = correlationId;

        var captured = await SendThroughHandlerAsync(httpContext, request => request);

        Assert.Equal(correlationId, Assert.Single(captured.Headers.GetValues(CorrelationIdMiddlewareExtensions.HeaderName)));
    }

    [Fact]
    public async Task DelegatingHandler_WithExistingHeader_ReplacesItWithTheCurrentRequestId()
    {
        var correlationId = Guid.NewGuid().ToString();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddlewareExtensions.HeaderName] = correlationId;

        var captured = await SendThroughHandlerAsync(httpContext, request =>
        {
            request.Headers.Add(CorrelationIdMiddlewareExtensions.HeaderName, "stale-value");
            return request;
        });

        Assert.Equal(correlationId, Assert.Single(captured.Headers.GetValues(CorrelationIdMiddlewareExtensions.HeaderName)));
    }

    [Fact]
    public async Task DelegatingHandler_WithoutHttpContext_LeavesRequestUnchanged()
    {
        var captured = await SendThroughHandlerAsync(httpContext: null, request => request);

        Assert.False(captured.Headers.Contains(CorrelationIdMiddlewareExtensions.HeaderName));
    }

    [Fact]
    public async Task UseCorrelationId_WithValidInboundHeader_PreservesIt()
    {
        var inbound = Guid.NewGuid().ToString();

        var context = await RunCorrelationMiddlewareAsync(inbound);

        Assert.Equal(inbound, context.Items[CorrelationIdMiddlewareExtensions.HeaderName]);
        Assert.Equal(inbound, context.Response.Headers[CorrelationIdMiddlewareExtensions.HeaderName]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task UseCorrelationId_WithMissingOrMalformedHeader_GeneratesNewGuid(string? inbound)
    {
        var context = await RunCorrelationMiddlewareAsync(inbound);

        var assigned = Assert.IsType<string>(context.Items[CorrelationIdMiddlewareExtensions.HeaderName]);
        Assert.True(Guid.TryParse(assigned, out _));
        Assert.NotEqual(inbound, assigned);
    }

    private static async Task<HttpRequestMessage> SendThroughHandlerAsync(
        HttpContext? httpContext,
        Func<HttpRequestMessage, HttpRequestMessage> configureRequest)
    {
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var capturingHandler = new CapturingHandler();
        var handler = new CorrelationIdDelegatingHandler(accessor) { InnerHandler = capturingHandler };
        using var client = new HttpClient(handler);

        var request = configureRequest(new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test"));
        using var response = await client.SendAsync(request, CancellationToken.None);

        return capturingHandler.Request!;
    }

    private static async Task<HttpContext> RunCorrelationMiddlewareAsync(string? inboundHeader)
    {
        var services = new ServiceCollection();
        var builder = new ApplicationBuilder(services.BuildServiceProvider());
        builder.UseCorrelationId();
        builder.Run(_ => Task.CompletedTask);
        var pipeline = builder.Build();

        var context = new DefaultHttpContext();
        if (inboundHeader is not null)
        {
            context.Request.Headers[CorrelationIdMiddlewareExtensions.HeaderName] = inboundHeader;
        }

        await pipeline(context);
        return context;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
