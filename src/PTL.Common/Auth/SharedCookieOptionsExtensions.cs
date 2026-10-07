using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace PTL.Common.Auth;

/// <summary>
/// Shared cookie configuration - identical across every PTL OIDC-backed web front-end's
/// <c>AddCookie</c> setup, aside from <c>EventsType</c>, which each caller sets afterwards to its
/// own provider-specific events class.
/// </summary>
public static class SharedCookieOptionsExtensions
{
    public static void ConfigureSharedCookieOptions(this CookieAuthenticationOptions options, bool useLocalHttpFriendlyOidcSettings)
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = useLocalHttpFriendlyOidcSettings
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        // The provider's OIDC callback is a same-site top-level navigation once it lands on our own
        // /signin-oidc - the app's own session cookie itself doesn't need SameSite=None.
        options.Cookie.SameSite = SameSiteMode.Lax;
    }
}
