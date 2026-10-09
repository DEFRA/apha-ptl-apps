using System.Security.Claims;
using PTL.ApiClient;
using PTL.Auth.Entra.Claims;
using PTL.Auth.Entra.Events;
using PTL.Contracts.InternalUser;

namespace PTL.InternalWeb.Features.Account;

/// <summary>
/// Resolves an Entra ID-authenticated internal user against PTL.Api's tblUsers row, once per
/// sign-in (called from <c>EntraOpenIdConnectEvents.TokenValidated</c>, before the local cookie is
/// written). Unlike the external-user CIDM flow, there is no "limited access" allowance - a user
/// with no matching tblUsers row is denied sign-in entirely, per the explicit internal-access
/// requirement for PT-LIMS.
/// </summary>
/// <param name="internalUserApiClient">Calls PTL.Api's internal-user resolve endpoint.</param>
/// <param name="configuration">Used only to read the local-testing bypass flag below.</param>
public sealed class InternalUserResolver(IInternalUserApiClient internalUserApiClient, IConfiguration configuration) : IEntraInternalUserResolver
{
    /// <inheritdoc />
    public async Task<EntraInternalUserResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // 'oid' (directory object id) is the Microsoft-recommended stable per-user identifier -
        // unlike 'sub', which is pairwise/per-application by default on the v2.0 endpoint. Default
        // inbound JWT claim mapping sometimes remaps it to the long schema URI, checked as a
        // fallback; raw "oid" is checked first in case mapping is ever turned off.
        var entraSsoIdClaim = principal.FindFirst(EntraClaimTypes.ObjectId)?.Value
            ?? principal.FindFirst(EntraClaimTypes.ObjectIdLongClaimUri)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(entraSsoIdClaim, out var entraSsoId))
        {
            return EntraInternalUserResolution.Deny("/Account/NotPermitted");
        }

        // Requires the Entra ID app registration to configure onprem_samaccountname and
        // onprem_domainname as optional ID token claims (Azure Portal: App registration -> Token
        // configuration -> Add optional claim -> ID token) - they are not present by default.
        var samAccountName = principal.FindFirst(EntraClaimTypes.OnPremisesSamAccountName)?.Value ?? string.Empty;

        // AD Connect can sync this as either NetBIOS ("DEFRA") or FQDN ("DT2.local") depending on
        // the tenant's sync configuration - tblUsers.fldUsername was always populated with the
        // short NetBIOS-style prefix (from the legacy Windows-auth days), so truncate at the first
        // '.' to match regardless of which form this tenant actually sends.
        var rawDomainName = principal.FindFirst(EntraClaimTypes.OnPremisesDomainName)?.Value ?? string.Empty;
        var domainName = rawDomainName.Split('.')[0];
        var username = !string.IsNullOrEmpty(domainName) && !string.IsNullOrEmpty(samAccountName)
            ? $"{domainName}\\{samAccountName}"
            : samAccountName;

        if (string.IsNullOrEmpty(username))
        {
            return EntraInternalUserResolution.Deny("/Account/NotPermitted");
        }

        var response = await internalUserApiClient.ResolveAsync(
            new ResolveInternalUserRequest(entraSsoId, username),
            cancellationToken);

        if (!response.IsPermitted)
        {
            // LOCAL-TESTING BYPASS - grants a synthetic Admin identity instead of denying sign-in,
            // so System Administration pages are reachable without a seeded tblUsers row. This is
            // an auth bypass: it lets ANY signed-in Entra user into PT-LIMS as Admin, with no
            // database record at all. Safe to leave in source control only because it is OFF by
            // default - it activates solely via the "Entra:BypassUserResolutionForLocalTesting"
            // flag, which must be set in a developer's own user-secrets/environment and must never
            // be added to appsettings.json or any other committed config. Do not enable this flag
            // outside a local dev machine.
            if (configuration.GetValue<bool>("Entra:BypassUserResolutionForLocalTesting"))
            {
                return EntraInternalUserResolution.Allow(new Dictionary<string, string>
                {
                    [InternalUserClaimTypes.InternalUserId] = Guid.Empty.ToString(),
                    [InternalUserClaimTypes.FullName] = "Local Test User (bypass)",
                    [InternalUserClaimTypes.Department] = "N/A",
                    [InternalUserClaimTypes.ResolvedRoles] = "Admin"
                });
            }

            return EntraInternalUserResolution.Deny("/Account/NotPermitted");
        }

        return EntraInternalUserResolution.Allow(new Dictionary<string, string>
        {
            [InternalUserClaimTypes.InternalUserId] = response.UserId!.Value.ToString(),
            [InternalUserClaimTypes.FullName] = response.FullName,
            [InternalUserClaimTypes.Department] = response.Department,
            [InternalUserClaimTypes.ResolvedRoles] = string.Join(',', response.Roles)
        });
    }
}
