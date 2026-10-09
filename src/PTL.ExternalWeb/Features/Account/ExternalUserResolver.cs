using System.Security.Claims;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Auth.Cidm.Claims;
using PTL.Auth.Cidm.Events;
using PTL.Contracts.ExternalUser;

namespace PTL.ExternalWeb.Features.Account;

/// <summary>
/// Resolves a CIDM-authenticated external user against PTL.Api's Participant/Viewer/Test
/// Consultant tables, once per sign-in (called from <c>CidmOpenIdConnectEvents.TokenValidated</c>,
/// before the local cookie is written). Adds <see cref="ExternalUserClaimTypes.DisplayName"/> and
/// <see cref="ExternalUserClaimTypes.ResolvedRoles"/> so the landing page never needs to call
/// PTL.Api again to know who the user is.
/// </summary>
/// <param name="externalUserApiClient">Calls PTL.Api's external-user resolve endpoint.</param>
/// <param name="logger">Logs why a sign-in was denied, for support/diagnostics.</param>
public sealed class ExternalUserResolver(IExternalUserApiClient externalUserApiClient, ILogger<ExternalUserResolver> logger) : ICidmExternalUserResolver
{
    /// <inheritdoc />
    public async Task<CidmExternalUserResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // contactId is CIDM's stable identifier for the person, independent of which
        // organisation/relationship they're currently acting under.
        if (!Guid.TryParse(principal.FindFirst(CidmClaimTypes.ContactId)?.Value, out var ssoIdExt))
        {
            logger.LogWarning("CIDM resolve denied: no parseable {Claim} claim present", CidmClaimTypes.ContactId);
            return CidmExternalUserResolution.Deny("/Account/NotPermitted");
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value ?? string.Empty;
        var displayName = ResolveDisplayName(principal);

        // CidmOpenIdConnectEvents.TokenValidated already added a standard ClaimTypes.Role claim for
        // each raw CIDM role ("Participant", "Viewer", "Test Consultant", ...) before calling this
        // resolver - a user can hold more than one simultaneously.
        var cidmRoles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct().ToList();

        var response = await externalUserApiClient.ResolveAsync(
            new ResolveExternalUserRequest(ssoIdExt, email, displayName, cidmRoles),
            cancellationToken);

        // No CIDM role resolved to an existing record - nothing in PTL this person is permitted to
        // use, so deny entirely rather than signing them into an app with no accessible data.
        if (response.Roles.Count == 0)
        {
            return CidmExternalUserResolution.Deny("/Account/NotPermitted");
        }

        var claims = new Dictionary<string, string>
        {
            [ExternalUserClaimTypes.DisplayName] = response.DisplayName,
            [ExternalUserClaimTypes.ResolvedRoles] = string.Join(',', response.Roles)
        };

        if (!string.IsNullOrWhiteSpace(response.LabCode))
        {
            claims[ExternalUserClaimTypes.LabCode] = response.LabCode;
        }

        if (response.CanOrderOnline)
        {
            claims[ExternalUserClaimTypes.CanOrderOnline] = "true";
        }

        return CidmExternalUserResolution.Allow(claims);
    }

    // CIDM's display-name claim set isn't fully confirmed for PT-LIMS's registration, so this
    // tries the same conventions CDC's resolver tries (firstName/lastName), then falls back to
    // more standard OIDC claim names, before giving up to an empty string.
    private static string ResolveDisplayName(ClaimsPrincipal principal)
    {
        var firstName = principal.FindFirst("firstName")?.Value ?? principal.FindFirst(ClaimTypes.GivenName)?.Value;
        var lastName = principal.FindFirst("lastName")?.Value ?? principal.FindFirst(ClaimTypes.Surname)?.Value;
        var fullName = string.Join(' ', new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return principal.FindFirst("name")?.Value ?? principal.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
    }
}
