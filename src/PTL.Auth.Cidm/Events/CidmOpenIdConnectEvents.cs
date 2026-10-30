using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
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
public sealed class CidmOpenIdConnectEvents : OidcProviderEvents<ICidmExternalUserResolver, CidmExternalUserResolution>
{
    // Claims the app reads after sign-in, on top of the common identity claims. The raw relationships/roles
    // arrays are dropped because the parsed claims below carry the same data.
    private static readonly IReadOnlySet<string> RetainedClaimTypes = OpenIdConnectEventHelpers.RetainedClaims(
        ClaimTypes.Role,
        "firstName",
        "lastName",
        CidmClaimTypes.CurrentRelationshipId,
        CidmClaimTypes.Relationship,
        CidmClaimTypes.Role);

    private readonly CidmOptions _cidmOptions;

    public CidmOpenIdConnectEvents(IOptions<CidmOptions> cidmOptions, ILogger<CidmOpenIdConnectEvents> logger)
        : base(logger, "CIDM", RetainedClaimTypes)
    {
        _cidmOptions = cidmOptions.Value;
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

    protected override void MapClaims(ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        var rawRelationships = principal.FindAll(CidmClaimTypes.RawRelationships).Select(c => c.Value);
        foreach (var relationship in CidmClaimsMapper.ParseRelationships(rawRelationships))
        {
            identity.AddClaim(new Claim(CidmClaimTypes.Relationship, JsonSerializer.Serialize(relationship)));
        }

        // "role" is a well-known short JWT claim name, so ASP.NET Core's default inbound claim-type
        // mapping has already renamed it to ClaimTypes.Role (keeping its raw, unparsed
        // "relationshipId:roleName:status" value) by the time this handler runs - CidmClaimTypes.RawRoles
        // only ever matches in tests that build the principal directly, never against a real CIDM token.
        var rawRoleClaims = principal.FindAll(CidmClaimTypes.RawRoles)
            .Concat(principal.FindAll(ClaimTypes.Role))
            .ToList();
        foreach (var rawRoleClaim in rawRoleClaims)
        {
            identity.RemoveClaim(rawRoleClaim);
        }

        foreach (var role in CidmClaimsMapper.ParseRoles(rawRoleClaims.Select(c => c.Value)))
        {
            // Standard role claim so [Authorize(Roles = "...")] works against the role name directly,
            // plus the full parsed detail (including which relationship/organisation it applies to).
            identity.AddClaim(new Claim(ClaimTypes.Role, role.RoleName));
            identity.AddClaim(new Claim(CidmClaimTypes.Role, JsonSerializer.Serialize(role)));
        }
    }

    protected override Task<CidmExternalUserResolution> ResolveAsync(
        ICidmExternalUserResolver resolver, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        resolver.ResolveAsync(principal, cancellationToken);

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
            // Only the redirect target goes in state - the id_token in Properties would push /signout-oidc past the WAF query-string limit.
            var stateProperties = new AuthenticationProperties { RedirectUri = context.Properties.RedirectUri };
            context.ProtocolMessage.State = context.Options.StateDataFormat.Protect(stateProperties);
        }

        context.HandleResponse();
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(CidmSignOutFormWriter.WriteForm(
            endSessionEndpoint,
            context.ProtocolMessage.IdTokenHint,
            context.ProtocolMessage.PostLogoutRedirectUri,
            context.ProtocolMessage.State));
    }

    public override Task SignedOutCallbackRedirect(RemoteSignOutContext context)
    {
        // HandleSignOutCallbackAsync only redirects if Properties.RedirectUri is set here - CIDM's
        // end-session policy does not always echo the protected state back on its redirect to
        // SignedOutCallbackPath, which otherwise leaves this blank and renders an empty page.
        if (string.IsNullOrEmpty(context.Properties?.RedirectUri))
        {
            context.Properties ??= new AuthenticationProperties();
            context.Properties.RedirectUri = "/Account/SignedOut";
        }

        return Task.CompletedTask;
    }
}
