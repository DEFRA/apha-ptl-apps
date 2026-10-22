using System.Security.Claims;
using Microsoft.Extensions.Logging;
using PTL.Auth.Entra.Claims;
using PTL.Common.Auth;

namespace PTL.Auth.Entra.Events;

/// <summary>
/// Entra ID-specific hooks into the OpenID Connect handler: resolves the signed-in identity to the
/// application's own user record, and hides OIDC failure detail from the user.
/// </summary>
public sealed class EntraOpenIdConnectEvents(ILogger<EntraOpenIdConnectEvents> logger)
    : OidcProviderEvents<IEntraInternalUserResolver, EntraInternalUserResolution>(logger, "Entra ID", RetainedClaimTypes)
{
    // Claims the app reads after sign-in, on top of the common identity claims. onprem_samaccountname/
    // onprem_domainname must be retained (not just read once here) - EntraCookieEvents re-invokes
    // IEntraInternalUserResolver.ResolveAsync on the cookie's principal to keep roles current, and
    // InternalUserResolver needs both claims to re-derive the username every time it runs, not only
    // at the original sign-in.
    private static readonly IReadOnlySet<string> RetainedClaimTypes = OpenIdConnectEventHelpers.RetainedClaims(
        EntraClaimTypes.ObjectId,
        EntraClaimTypes.ObjectIdLongClaimUri,
        EntraClaimTypes.OnPremisesSamAccountName,
        EntraClaimTypes.OnPremisesDomainName,
        "sid",
        "displayname",
        "preferred_username");

    protected override Task<EntraInternalUserResolution> ResolveAsync(
        IEntraInternalUserResolver resolver, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        resolver.ResolveAsync(principal, cancellationToken);
}
