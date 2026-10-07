using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Auth.Cidm.Options;
using PTL.Common.Auth;

namespace PTL.Auth.Cidm.TokenRefresh;

/// <summary>
/// Refreshes CIDM tokens ahead of access token expiry on the app's own session cookie, so a request never
/// proceeds with a token known to be expired or about to expire. On refresh failure (network error, or the
/// refresh_token itself has exceeded its lifetime) the principal is rejected, forcing a fresh interactive
/// sign-in on the next request.
/// </summary>
public sealed partial class CidmCookieEvents : CookieAuthenticationEvents
{
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

    public override Task ValidatePrincipal(CookieValidatePrincipalContext context) =>
        TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context,
            _cidmOptions.RefreshBeforeExpiry,
            _cidmOptions.CallbackPath,
            _timeProvider,
            _refreshService.RefreshAsync,
            LogRefreshFailed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "CIDM token refresh failed for the current session - rejecting the principal")]
    private partial void LogRefreshFailed();
}
