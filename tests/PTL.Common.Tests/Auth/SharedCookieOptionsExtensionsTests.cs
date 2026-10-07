using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using PTL.Common.Auth;

namespace PTL.Common.Tests.Auth;

public class SharedCookieOptionsExtensionsTests
{
    [Fact]
    public void ConfigureSharedCookieOptions_SetsSharedProperties()
    {
        var options = new CookieAuthenticationOptions();

        options.ConfigureSharedCookieOptions(useLocalHttpFriendlyOidcSettings: false);

        Assert.Equal("/Account/Login", options.LoginPath);
        Assert.Equal("/Account/Logout", options.LogoutPath);
        Assert.True(options.SlidingExpiration);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
    }

    [Fact]
    public void ConfigureSharedCookieOptions_NotLocalHttpFriendly_RequiresSecureCookie()
    {
        var options = new CookieAuthenticationOptions();

        options.ConfigureSharedCookieOptions(useLocalHttpFriendlyOidcSettings: false);

        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
    }

    [Fact]
    public void ConfigureSharedCookieOptions_LocalHttpFriendly_AllowsSameAsRequestCookie()
    {
        var options = new CookieAuthenticationOptions();

        options.ConfigureSharedCookieOptions(useLocalHttpFriendlyOidcSettings: true);

        Assert.Equal(CookieSecurePolicy.SameAsRequest, options.Cookie.SecurePolicy);
    }
}
