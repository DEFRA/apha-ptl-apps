using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Auth.Entra.Options;
using PTL.Common.Auth;

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

    public override Task ValidatePrincipal(CookieValidatePrincipalContext context) =>
        TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context,
            _entraOptions.RefreshBeforeExpiry,
            _entraOptions.CallbackPath,
            _timeProvider,
            _refreshService.RefreshAsync,
            LogRefreshFailed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entra ID token refresh failed for the current session - rejecting the principal")]
    private partial void LogRefreshFailed();
}
