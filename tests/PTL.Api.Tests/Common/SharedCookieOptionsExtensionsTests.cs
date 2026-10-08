using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using PTL.Common.Auth;

namespace PTL.Api.Tests.Common;

public class SharedCookieOptionsExtensionsTests
{
    [Fact]
    public void ConfigureSharedCookieOptions_WithLocalHttpFriendlySettings_UsesSameAsRequest()
    {
        var options = new CookieAuthenticationOptions();

        options.ConfigureSharedCookieOptions(useLocalHttpFriendlyOidcSettings: true);

        Assert.Equal(CookieSecurePolicy.SameAsRequest, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
        Assert.True(options.Cookie.HttpOnly);
        Assert.True(options.SlidingExpiration);
        Assert.Equal("/Account/Login", options.LoginPath);
        Assert.Equal("/Account/Logout", options.LogoutPath);
    }

    [Fact]
    public void ConfigureSharedCookieOptions_WithoutLocalHttpFriendlySettings_RequiresSecureAlways()
    {
        var options = new CookieAuthenticationOptions();

        options.ConfigureSharedCookieOptions(useLocalHttpFriendlyOidcSettings: false);

        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
    }
}
