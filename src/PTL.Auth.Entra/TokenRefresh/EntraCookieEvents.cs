using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Auth.Entra.Events;
using PTL.Auth.Entra.Options;
using PTL.Common.Auth;

namespace PTL.Auth.Entra.TokenRefresh;

/// <summary>
/// Refreshes Entra ID tokens ahead of access token expiry on the app's own session cookie, so a
/// request never proceeds with a token known to be expired or about to expire. On refresh failure
/// (network error, or the refresh_token itself has exceeded its lifetime, or the user's account was
/// disabled/removed in Entra ID) the principal is rejected, forcing a fresh interactive sign-in on
/// the next request. Also keeps the signed-in user's PTL roles in step with tblUsers/tlnkUserRoles
/// (see <see cref="RevalidateRolesAsync"/>) - without this, a role change made via System
/// Administration would only take effect the next time the user fully signs out and back in.
/// </summary>
public sealed partial class EntraCookieEvents : CookieAuthenticationEvents
{
    // Reuses the AuthenticationProperties token bag (alongside expires_at/refresh_token) rather than
    // Properties.Items, purely for consistency with how TokenRefreshCookieEventsHelper already
    // tracks state here - this value is never actually a token.
    private const string RolesCheckedAtTokenName = "ptl_roles_checked_at";

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
        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context,
            _entraOptions.RefreshBeforeExpiry,
            _entraOptions.CallbackPath,
            _timeProvider,
            _refreshService.RefreshAsync,
            LogRefreshFailed);

        if (context.Principal is not null)
        {
            await RevalidateRolesAsync(context);
        }
    }

    // InternalUserResolver only ever runs once, at sign-in, so the ResolvedRoles claim it writes
    // would otherwise sit frozen on the cookie until the user signs out - a System Administration
    // role change wouldn't reach them without a fresh sign-in (or, with no interactive way to force
    // that, an app restart). Re-running the same resolver here, throttled to
    // RoleRevalidationInterval, keeps it current without needing either.
    private async Task RevalidateRolesAsync(CookieValidatePrincipalContext context)
    {
        if (context.Principal!.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var checkedAtRaw = context.Properties.GetTokenValue(RolesCheckedAtTokenName);
        if (checkedAtRaw is not null &&
            DateTimeOffset.TryParse(checkedAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var checkedAt) &&
            _timeProvider.GetUtcNow() - checkedAt < _entraOptions.RoleRevalidationInterval)
        {
            return;
        }

        var resolver = context.HttpContext.RequestServices.GetService<IEntraInternalUserResolver>();
        if (resolver is null)
        {
            return;
        }

        EntraInternalUserResolution resolution;
        try
        {
            resolution = await resolver.ResolveAsync(context.Principal, context.HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // PTL.Api being briefly unreachable must not sign the user out of an otherwise-valid
            // session - the roles simply stay as last known until the next check succeeds.
            LogRoleRevalidationFailed(exception);
            return;
        }

        SetRolesCheckedAt(context.Properties, _timeProvider.GetUtcNow());

        if (!resolution.IsAllowed)
        {
            LogRoleRevalidationDenied();
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        var changed = false;
        foreach (var (claimType, claimValue) in resolution.Claims ?? new Dictionary<string, string>())
        {
            var existing = identity.FindFirst(claimType);
            if (existing?.Value == claimValue)
            {
                continue;
            }

            changed = true;
            if (existing is not null)
            {
                identity.RemoveClaim(existing);
            }

            identity.AddClaim(new Claim(claimType, claimValue));
        }

        if (changed)
        {
            context.ShouldRenew = true;
        }
    }

    // AuthenticationProperties.UpdateTokenValue only ever updates a token that already exists in
    // the list - it silently no-ops for a name it has never seen before, so the first-ever check
    // has to add the token itself rather than update it.
    private static void SetRolesCheckedAt(AuthenticationProperties properties, DateTimeOffset value)
    {
        var tokens = properties.GetTokens()?.ToList() ?? [];
        tokens.RemoveAll(token => token.Name == RolesCheckedAtTokenName);
        tokens.Add(new AuthenticationToken { Name = RolesCheckedAtTokenName, Value = value.ToString("o", CultureInfo.InvariantCulture) });
        properties.StoreTokens(tokens);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entra ID token refresh failed for the current session - rejecting the principal")]
    private partial void LogRefreshFailed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "PTL role re-validation could not reach PTL.Api for the current session - keeping the last known roles")]
    private partial void LogRoleRevalidationFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PTL role re-validation denied the current session - rejecting the principal")]
    private partial void LogRoleRevalidationDenied();
}
