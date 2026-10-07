using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PTL.Common.Auth;

namespace PTL.Auth.Entra.Events;

/// <summary>
/// Entra ID-specific hooks into the OpenID Connect handler: resolves the signed-in identity to the
/// application's own user record, and hides OIDC failure detail from the user.
/// </summary>
public sealed partial class EntraOpenIdConnectEvents : OpenIdConnectEvents
{
    private readonly ILogger<EntraOpenIdConnectEvents> _logger;

    public EntraOpenIdConnectEvents(ILogger<EntraOpenIdConnectEvents> logger)
    {
        _logger = logger;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        // Resolved per-request rather than constructor-injected (this class is a singleton) - if
        // the consuming app hasn't registered one (or RequestServices isn't set, as in some unit
        // test contexts), sign-in proceeds exactly as before this hook existed.
        var resolver = context.HttpContext.RequestServices?.GetService<IEntraInternalUserResolver>();
        Func<ClaimsPrincipal, CancellationToken, Task<IIdentityResolution>>? resolve = resolver is null
            ? null
            : async (principal, ct) => await resolver.ResolveAsync(principal, ct);

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, resolve);
    }

    public override Task RemoteFailure(RemoteFailureContext context) =>
        OpenIdConnectEventHelpers.HandleRemoteFailureAsync(context, LogAuthenticationFailed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Entra ID authentication failed")]
    private partial void LogAuthenticationFailed(Exception? exception);
}
