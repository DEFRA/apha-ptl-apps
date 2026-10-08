using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PTL.Common.Auth;

/// <summary>
/// Shared cookie-validation token-refresh logic - identical across every PTL auth provider except
/// for which options/service/log-message the caller supplies.
/// </summary>
public static class TokenRefreshCookieEventsHelper
{
    private const string ExpiresAtTokenName = "expires_at";
    private const string RefreshTokenName = "refresh_token";
    private const string AccessTokenName = "access_token";
    private const string IdTokenName = "id_token";

    /// <summary>
    /// Refreshes the provider's tokens ahead of access token expiry on the app's own session
    /// cookie, so a request never proceeds with a token known to be expired or about to expire. On
    /// refresh failure (network error, the refresh_token itself has exceeded its lifetime, or the
    /// user's account was disabled/removed with the provider) the principal is rejected, forcing a
    /// fresh interactive sign-in on the next request.
    /// </summary>
    public static async Task ValidatePrincipalAsync<TResult>(
        CookieValidatePrincipalContext context,
        TimeSpan refreshBeforeExpiry,
        string callbackPath,
        TimeProvider timeProvider,
        Func<string, string, CancellationToken, Task<TResult>> refreshAsync,
        Action logRefreshFailed)
        where TResult : ITokenRefreshResult
    {
        var expiresAtRaw = context.Properties.GetTokenValue(ExpiresAtTokenName);
        var refreshToken = context.Properties.GetTokenValue(RefreshTokenName);

        if (expiresAtRaw is null || refreshToken is null ||
            !DateTimeOffset.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiresAt))
        {
            // No refreshable token metadata on this cookie - leave the existing principal as-is
            // rather than forcing a sign-out over something this feature doesn't apply to.
            return;
        }

        if (expiresAt - timeProvider.GetUtcNow() > refreshBeforeExpiry)
        {
            return;
        }

        var redirectUri = $"{context.Request.Scheme}://{context.Request.Host}{callbackPath}";
        var result = await refreshAsync(refreshToken, redirectUri, context.HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            logRefreshFailed();
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        context.Properties.UpdateTokenValue(AccessTokenName, result.AccessToken!);
        if (result.IdToken is not null)
        {
            context.Properties.UpdateTokenValue(IdTokenName, result.IdToken);
        }

        context.Properties.UpdateTokenValue(RefreshTokenName, result.RefreshToken ?? refreshToken);
        context.Properties.UpdateTokenValue(ExpiresAtTokenName, result.ExpiresAt!.Value.ToString("o", CultureInfo.InvariantCulture));

        context.ShouldRenew = true;
    }
}
