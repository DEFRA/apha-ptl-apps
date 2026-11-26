using Microsoft.AspNetCore.Http;
using PTL.Common.Security;

namespace PTL.Common.Tests.Security;

public class CspNonceTests
{
    [Fact]
    public void Create_StoresTheNonceForTheRequest()
    {
        var context = new DefaultHttpContext();

        var nonce = CspNonce.Create(context);

        Assert.Equal(nonce, CspNonce.Get(context));
    }

    [Fact]
    public void Create_IsA128BitHexValue_NewEveryTime()
    {
        var context = new DefaultHttpContext();

        var first = CspNonce.Create(context);
        var second = CspNonce.Create(context);

        Assert.Matches("^[0-9a-f]{32}$", first);
        Assert.NotEqual(first, second);
        Assert.Equal(second, CspNonce.Get(context));
    }

    [Fact]
    public void Get_WithoutANonceOrRequest_ReturnsNull()
    {
        Assert.Null(CspNonce.Get(new DefaultHttpContext()));
        Assert.Null(CspNonce.Get(null));
    }
}
