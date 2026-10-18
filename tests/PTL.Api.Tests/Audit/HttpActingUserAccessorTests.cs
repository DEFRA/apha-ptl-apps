using Microsoft.AspNetCore.Http;
using PTL.Api.Infrastructure;
using PTL.Common.Audit;

namespace PTL.Api.Tests.Audit;

public class HttpActingUserAccessorTests
{
    private static HttpActingUserAccessor CreateAccessor(string? headerValue)
    {
        var context = new DefaultHttpContext();
        if (headerValue is not null)
        {
            context.Request.Headers[ActingUser.HeaderName] = headerValue;
        }

        return new HttpActingUserAccessor(new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public void ValidHeader_ReturnsTheUserId()
    {
        var userId = Guid.NewGuid();

        Assert.Equal(userId, CreateAccessor(userId.ToString()).ActingUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void MissingOrMalformedHeader_ReturnsNull(string? headerValue)
    {
        Assert.Null(CreateAccessor(headerValue).ActingUserId);
    }

    [Fact]
    public void NoHttpContext_ReturnsNull()
    {
        var accessor = new HttpActingUserAccessor(new HttpContextAccessor());

        Assert.Null(accessor.ActingUserId);
    }
}
