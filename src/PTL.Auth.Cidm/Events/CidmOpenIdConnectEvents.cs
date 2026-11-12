using System.Security.Claims;
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
/// CIDM-specific hooks into the OpenID Connect handler: injects the non-standard serviceId parameter, rejects tokens whose serviceId is not the configured one,
/// hides OIDC failure detail from the user, and renders the POST-based sign-out form CIDM recommends.
/// The user's role is never read from the token - it comes from the PTL record the resolver matches.
/// </summary>
public sealed partial class CidmOpenIdConnectEvents : OidcProviderEvents<ICidmExternalUserResolver, CidmExternalUserResolution>
{
    // Claims the app reads after sign-in, on top of the common identity claims. Everything else in the
    // token (roles, relationships, assurance levels, ...) is dropped to keep the session cookie small.
    private static readonly IReadOnlySet<string> RetainedClaimTypes = OpenIdConnectEventHelpers.RetainedClaims(
        "firstName",
        "lastName",
        CidmClaimTypes.ContactId);

    private readonly CidmOptions _cidmOptions;
    private readonly ILogger<CidmOpenIdConnectEvents> _logger;

    public CidmOpenIdConnectEvents(IOptions<CidmOptions> cidmOptions, ILogger<CidmOpenIdConnectEvents> logger)
        : base(logger, "CIDM", RetainedClaimTypes)
    {
        _cidmOptions = cidmOptions.Value;
        _logger = logger;
    }

    public override Task TokenValidated(TokenValidatedContext context)
    {
        // Before anything else: a token issued for a different CIDM service must never become a session.
        if (!HasConfiguredServiceId(context.Principal))
        {
            LogServiceIdMismatch(context.Principal?.FindFirst(CidmClaimTypes.ServiceId)?.Value);
            OpenIdConnectEventHelpers.DenyWithErrorPage(context);
            return Task.CompletedTask;
        }

        return base.TokenValidated(context);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "CIDM sign-in denied: the token's serviceId '{TokenServiceId}' is missing or does not match the configured service id")]
    private partial void LogServiceIdMismatch(string? tokenServiceId);

    // Fails closed: an unconfigured service id matches nothing, rather than a token that also has none.
    private bool HasConfiguredServiceId(ClaimsPrincipal? principal)
    {
        var configured = _cidmOptions.ServiceId.Trim();
        var actual = principal?.FindFirst(CidmClaimTypes.ServiceId)?.Value?.Trim();

        return !string.IsNullOrEmpty(configured) && string.Equals(actual, configured, StringComparison.OrdinalIgnoreCase);
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
