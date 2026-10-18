using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PTL.Common.Audit;

namespace PTL.Common.Tests.Audit;

public class ActingUserDelegatingHandlerTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage());
        }
    }

    private static async Task<HttpRequestMessage> SendAsync(HttpContext? httpContext)
    {
        var inner = new CapturingHandler();
        var handler = new ActingUserDelegatingHandler(new HttpContextAccessor { HttpContext = httpContext }) { InnerHandler = inner };

        await new HttpMessageInvoker(handler).SendAsync(new HttpRequestMessage(HttpMethod.Post, "http://api/test"), CancellationToken.None);

        return inner.Request!;
    }

    [Fact]
    public async Task SignedInUser_IsForwardedInTheActingUserHeader()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ActingUser.ClaimType, userId.ToString())], "test"))
        };

        var request = await SendAsync(context);

        Assert.Equal(userId.ToString(), Assert.Single(request.Headers.GetValues(ActingUser.HeaderName)));
    }

    [Fact]
    public async Task NoSignedInUser_SendsNoHeader()
    {
        var request = await SendAsync(new DefaultHttpContext());

        Assert.False(request.Headers.Contains(ActingUser.HeaderName));
    }

    [Fact]
    public async Task NoHttpContext_SendsNoHeader()
    {
        var request = await SendAsync(httpContext: null);

        Assert.False(request.Headers.Contains(ActingUser.HeaderName));
    }
}
