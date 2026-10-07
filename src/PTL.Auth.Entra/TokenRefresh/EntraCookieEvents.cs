using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Auth.Entra.Options;

namespace PTL.Auth.Entra.TokenRefresh;

/// <summary>
/// Refreshes Entra ID tokens ahead of access token expiry on the app's own session cookie, so a
/// request never proceeds with a token known to be expired or about to expire. On refresh failure
/// (network error, or the refresh_token itself has exceeded its lifetime, or the user's account was
/// disabled/removed in Entra ID) the principal is rejected, forcing a fresh interactive sign-in on
/// the next request.
/// </summary>
public sealed partial class EntraCookieEvents : CookieAuthenticationEvents
{
    private const string ExpiresAtTokenName = "expires_at";
    private const string RefreshTokenName = "refresh_token";
    private const string AccessTokenName = "access_token";
    private const string IdTokenName = "id_token";

    private readonly IEntraTokenRefreshService _refreshService;
    private readonly EntraOptions _entraOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EntraCookieEvents> _logger;

    public EntraCookieEvents(
        IEntraTokenRefreshService refreshService,
        IOptions<EntraOptions> entraOptions,
        TimeProvider timeProvider,
        ILogger<EntraCookieEvents> logger)
    {
        _refreshService = refreshService;
        _entraOptions = entraOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var expiresAtRaw = context.Properties.GetTokenValue(ExpiresAtTokenName);
        var refreshToken = context.Properties.GetTokenValue(RefreshTokenName);

        if (expiresAtRaw is null || refreshToken is null ||
            !DateTimeOffset.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiresAt))
        {
            // No refreshable token metadata on this cookie - leave the existing principal as-is rather
            // than forcing a sign-out over something this feature doesn't apply to.
            return;
        }

        if (expiresAt - _timeProvider.GetUtcNow() > _entraOptions.RefreshBeforeExpiry)
        {
            return;
        }

        var redirectUri = BuildRedirectUri(context.Request);
        var result = await _refreshService.RefreshAsync(refreshToken, redirectUri, context.HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            LogRefreshFailed();
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

    private string BuildRedirectUri(HttpRequest request) =>
        $"{request.Scheme}://{request.Host}{_entraOptions.CallbackPath}";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entra ID token refresh failed for the current session - rejecting the principal")]
    private partial void LogRefreshFailed();
}
