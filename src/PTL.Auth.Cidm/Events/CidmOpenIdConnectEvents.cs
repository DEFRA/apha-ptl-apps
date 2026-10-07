using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Auth.Cidm.Claims;
using PTL.Auth.Cidm.Options;
using PTL.Common.Auth;

namespace PTL.Auth.Cidm.Events;

/// <summary>
/// CIDM-specific hooks into the OpenID Connect handler: injects the non-standard serviceId parameter,
/// maps the relationships/roles array claims, hides OIDC failure detail from the user, and renders the
/// POST-based sign-out form CIDM recommends.
/// </summary>
public sealed partial class CidmOpenIdConnectEvents : OpenIdConnectEvents
{
    private readonly CidmOptions _cidmOptions;
    private readonly ILogger<CidmOpenIdConnectEvents> _logger;

    public CidmOpenIdConnectEvents(IOptions<CidmOptions> cidmOptions, ILogger<CidmOpenIdConnectEvents> logger)
    {
        _cidmOptions = cidmOptions.Value;
        _logger = logger;
    }

    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        // serviceId is a DEFRA CIDM-specific parameter, not part of the OIDC spec, so the handler has no
        // built-in concept of it - it has to be added to the /authorize request manually.
        context.ProtocolMessage.SetParameter("serviceId", _cidmOptions.ServiceId);

        if (context.Properties.Items.TryGetValue(CidmAuthenticationDefaults.ForceReselectionProperty, out var forceReselection) &&
            forceReselection == "true")
        {
            context.ProtocolMessage.SetParameter("forceReselection", "true");
        }

        if (context.Properties.Items.TryGetValue(CidmAuthenticationDefaults.RelationshipIdProperty, out var relationshipId) &&
            !string.IsNullOrEmpty(relationshipId))
        {
            context.ProtocolMessage.SetParameter("relationshipId", relationshipId);
        }

        return Task.CompletedTask;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var rawRelationships = context.Principal.FindAll(CidmClaimTypes.RawRelationships).Select(c => c.Value);
        foreach (var relationship in CidmClaimsMapper.ParseRelationships(rawRelationships))
        {
            identity.AddClaim(new Claim(CidmClaimTypes.Relationship, JsonSerializer.Serialize(relationship)));
        }

        var rawRoles = context.Principal.FindAll(CidmClaimTypes.RawRoles).Select(c => c.Value);
        foreach (var role in CidmClaimsMapper.ParseRoles(rawRoles))
        {
            // Standard role claim so [Authorize(Roles = "...")] works against the role name directly,
            // plus the full parsed detail (including which relationship/organisation it applies to).
            identity.AddClaim(new Claim(ClaimTypes.Role, role.RoleName));
            identity.AddClaim(new Claim(CidmClaimTypes.Role, JsonSerializer.Serialize(role)));
        }

        // Resolved per-request rather than constructor-injected (this class is a singleton) - if
        // the consuming app hasn't registered one (or RequestServices isn't set, as in some unit
        // test contexts), sign-in proceeds exactly as before this hook existed.
        var resolver = context.HttpContext.RequestServices?.GetService<ICidmExternalUserResolver>();
        Func<ClaimsPrincipal, CancellationToken, Task<IIdentityResolution>>? resolve = resolver is null
            ? null
            : async (principal, ct) => await resolver.ResolveAsync(principal, ct);

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, resolve);
    }

    public override Task RemoteFailure(RemoteFailureContext context) =>
        OpenIdConnectEventHelpers.HandleRemoteFailureAsync(context, LogAuthenticationFailed);

    [LoggerMessage(Level = LogLevel.Error, Message = "CIDM authentication failed")]
    private partial void LogAuthenticationFailed(Exception? exception);

    public override async Task RedirectToIdentityProviderForSignOut(RedirectContext context)
    {
        var endSessionEndpoint = context.ProtocolMessage.IssuerAddress;
        if (string.IsNullOrEmpty(endSessionEndpoint))
        {
            return;
        }

        // The base handler only protects Properties into ProtocolMessage.State *after* this event
        // returns, as the very last step before it would normally issue the redirect itself - since
        // HandleResponse() below skips that step entirely, State is still empty at this point and must
        // be set here, or the round trip back to our own /signout-oidc (SignedOutCallbackPath) would
        // never be able to recover Properties.RedirectUri (/Account/SignedOut).
        if (string.IsNullOrEmpty(context.ProtocolMessage.State))
        {
            context.ProtocolMessage.State = context.Options.StateDataFormat.Protect(context.Properties);
        }

        context.HandleResponse();
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(CidmSignOutFormWriter.WriteForm(
            endSessionEndpoint,
            context.ProtocolMessage.IdTokenHint,
            context.ProtocolMessage.PostLogoutRedirectUri,
            context.ProtocolMessage.State));
    }
}
