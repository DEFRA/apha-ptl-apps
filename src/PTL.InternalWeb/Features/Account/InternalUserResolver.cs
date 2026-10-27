using System.Security.Claims;
using Microsoft.Extensions.Logging;
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
/// <param name="logger">Logs resolution failures so a sign-in landing on /Home/Error is diagnosable.</param>
public sealed partial class InternalUserResolver(IInternalUserApiClient internalUserApiClient, ILogger<InternalUserResolver> logger) : IEntraInternalUserResolver
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

        ResolveInternalUserResponse response;
        try
        {
            response = await internalUserApiClient.ResolveAsync(
                new ResolveInternalUserRequest(entraSsoId, username),
                cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            // HttpRequestException: network/DNS failure, connection refused, or EnsureSuccessStatusCode
            // threw for a non-success response. TaskCanceledException: request timeout. NotSupportedException:
            // response wasn't valid JSON for the expected DTO. Without this, a PTL.Api connectivity
            // problem during sign-in produces zero diagnostic trace - it just lands on /Home/Error.
            LogInternalUserResolutionFailed(entraSsoId, exception);
            return EntraInternalUserResolution.Deny("/Home/Error");
        }

        if (!response.IsPermitted)
        {
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to resolve internal user (SsoIdInt={SsoIdInt}) against PTL.Api")]
    private partial void LogInternalUserResolutionFailed(Guid ssoIdInt, Exception exception);
}
