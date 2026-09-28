using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Auth.Cidm.Options;

namespace PTL.Auth.Cidm.TokenRefresh;

/// <summary>
/// Refreshes CIDM tokens ahead of access token expiry on the app's own session cookie, so a request never
/// proceeds with a token known to be expired or about to expire. On refresh failure (network error, or the
/// refresh_token itself has exceeded its lifetime) the principal is rejected, forcing a fresh interactive
/// sign-in on the next request.
/// </summary>
public sealed partial class CidmCookieEvents : CookieAuthenticationEvents
{
    private const string ExpiresAtTokenName = "expires_at";
    private const string RefreshTokenName = "refresh_token";
    private const string AccessTokenName = "access_token";
    private const string IdTokenName = "id_token";

    private readonly ICidmTokenRefreshService _refreshService;
    private readonly CidmOptions _cidmOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CidmCookieEvents> _logger;

    public CidmCookieEvents(
        ICidmTokenRefreshService refreshService,
        IOptions<CidmOptions> cidmOptions,
        TimeProvider timeProvider,
        ILogger<CidmCookieEvents> logger)
    {
        _refreshService = refreshService;
        _cidmOptions = cidmOptions.Value;
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

        if (expiresAt - _timeProvider.GetUtcNow() > _cidmOptions.RefreshBeforeExpiry)
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
        $"{request.Scheme}://{request.Host}{_cidmOptions.CallbackPath}";

    [LoggerMessage(Level = LogLevel.Warning, Message = "CIDM token refresh failed for the current session - rejecting the principal")]
    private partial void LogRefreshFailed();
}
