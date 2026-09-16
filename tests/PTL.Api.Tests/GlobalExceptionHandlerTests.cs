using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Infrastructure;

namespace PTL.Api.Tests;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WritesProblemDetailsAndReturnsTrue()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(500, httpContext.Response.StatusCode);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("An unexpected error occurred.", body);
    }
}
