using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Auth.Cidm.Claims;
using PTL.Auth.Cidm.Options;

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

    public override Task TokenValidated(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return Task.CompletedTask;
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

        return Task.CompletedTask;
    }

    public override Task RemoteFailure(RemoteFailureContext context)
    {
        // Never surface raw OIDC/B2C failure detail to the user - log it server-side only and show a
        // generic error page, per the CIDM guide's own error-handling guidance.
        LogAuthenticationFailed(context.Failure);
        context.HandleResponse();
        context.Response.Redirect("/Home/Error");
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "CIDM authentication failed")]
    private partial void LogAuthenticationFailed(Exception? exception);

    public override async Task RedirectToIdentityProviderForSignOut(RedirectContext context)
    {
        var endSessionEndpoint = context.ProtocolMessage.IssuerAddress;
        if (string.IsNullOrEmpty(endSessionEndpoint))
        {
            return;
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
