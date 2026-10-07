using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        if (resolver is null)
        {
            return;
        }

        var resolution = await resolver.ResolveAsync(context.Principal, context.HttpContext.RequestAborted);
        if (!resolution.IsAllowed)
        {
            // Runs before the OIDC handler ever signs the principal into the cookie scheme, so
            // denying here means no local session is ever created for this sign-in attempt.
            context.HandleResponse();
            context.HttpContext.Response.Redirect(resolution.DenialRedirectPath ?? "/");
            return;
        }

        foreach (var (claimType, claimValue) in resolution.Claims ?? new Dictionary<string, string>())
        {
            identity.AddClaim(new Claim(claimType, claimValue));
        }
    }

    public override Task RemoteFailure(RemoteFailureContext context)
    {
        // Never surface raw OIDC/Entra ID failure detail to the user - log it server-side only and
        // show a generic error page.
        LogAuthenticationFailed(context.Failure);
        context.HandleResponse();
        context.Response.Redirect("/Home/Error");
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Entra ID authentication failed")]
    private partial void LogAuthenticationFailed(Exception? exception);
}
