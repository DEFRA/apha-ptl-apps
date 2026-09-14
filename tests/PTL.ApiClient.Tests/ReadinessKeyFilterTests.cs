using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PTL.ApiClient.Tests;

public class ReadinessKeyFilterTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsNotFound_WhenKeyNotConfigured()
    {
        var filter = new ReadinessKeyFilter(new ConfigurationBuilder().Build());
        var context = CreateContext(providedKey: "anything");

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("next"));

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsNotFound_WhenKeyWrong()
    {
        var filter = new ReadinessKeyFilter(BuildConfiguration("correct-key"));
        var context = CreateContext(providedKey: "wrong-key");

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("next"));

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenKeyCorrect()
    {
        var filter = new ReadinessKeyFilter(BuildConfiguration("correct-key"));
        var context = CreateContext(providedKey: "correct-key");

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("next"));

        Assert.Equal("next", result);
    }

    private static IConfiguration BuildConfiguration(string readinessKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["HealthCheck:ReadinessKey"] = readinessKey })
            .Build();

    private static EndpointFilterInvocationContext CreateContext(string providedKey)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[ReadinessKeyFilter.HeaderName] = providedKey;
        return EndpointFilterInvocationContext.Create(httpContext);
    }
}
