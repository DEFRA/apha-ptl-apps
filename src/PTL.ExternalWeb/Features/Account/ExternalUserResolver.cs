using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Polly;
using PTL.ApiClient;
using PTL.Auth.Cidm.Claims;
using PTL.Auth.Cidm.Events;
using PTL.Contracts.ExternalUser;

namespace PTL.ExternalWeb.Features.Account;

/// <summary>
/// Resolves a CIDM-authenticated external user against PTL.Api once per sign-in (called from
/// <c>CidmOpenIdConnectEvents.TokenValidated</c>, before the local cookie is written). Only the CIDM contact id,
/// email and name are sent - the user type and roles come from the PTL record PTL.Api matches, never from the
/// token. Adds the <see cref="ExternalUserClaimTypes"/> claims so the landing page never needs to call PTL.Api
/// again to know who the user is.
/// </summary>
/// <param name="externalUserApiClient">Calls PTL.Api's external-user resolve endpoint.</param>
/// <param name="logger">Logs resolution failures so a sign-in landing on /Home/Error is diagnosable.</param>
public sealed partial class ExternalUserResolver(IExternalUserApiClient externalUserApiClient, ILogger<ExternalUserResolver> logger) : ICidmExternalUserResolver
{
    /// <inheritdoc />
    public async Task<CidmExternalUserResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // contactId is CIDM's stable identifier for the person, independent of which
        // organisation/relationship they're currently acting under.
        if (!Guid.TryParse(principal.FindFirst(CidmClaimTypes.ContactId)?.Value, out var ssoIdExt) || ssoIdExt == Guid.Empty)
        {
            LogMissingContactIdClaim(CidmClaimTypes.ContactId);
            return CidmExternalUserResolution.Deny(NotPermittedReasons.PathFor());
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value ?? string.Empty;
        var displayName = ResolveDisplayName(principal);

        ResolveExternalUserResponse response;
        try
        {
            response = await externalUserApiClient.ResolveAsync(
                new ResolveExternalUserRequest(ssoIdExt, email, displayName),
                cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ExecutionRejectedException or NotSupportedException)
        {
            // HttpRequestException: network/DNS failure, connection refused, or EnsureSuccessStatusCode
            // threw for a non-success response. TaskCanceledException: request timeout. ExecutionRejectedException:
            // the standard resilience handler's total timeout (TimeoutRejectedException) or open circuit
            // breaker (BrokenCircuitException). NotSupportedException:
            // response wasn't valid JSON for the expected DTO. Without this, a PTL.Api connectivity
            // problem during sign-in produces zero diagnostic trace - it just lands on /Home/Error.
            LogExternalUserResolutionFailed(ssoIdExt, exception);
            return CidmExternalUserResolution.Deny("/Home/Error");
        }

        // The denial page only distinguishes "inactive" and "no active lab"; every other reason (no record, an
        // ambiguous email, an email already linked to someone else) shows the same generic message, so the page
        // never reveals whether an email address is known to PTL.
        return response.Status switch
        {
            ExternalUserResolutionStatus.Resolved => CidmExternalUserResolution.Allow(BuildClaims(response)),
            ExternalUserResolutionStatus.Inactive => CidmExternalUserResolution.Deny(NotPermittedReasons.PathFor(NotPermittedReasons.Inactive)),
            ExternalUserResolutionStatus.NoActiveLab => CidmExternalUserResolution.Deny(NotPermittedReasons.PathFor(NotPermittedReasons.NoActiveLab)),
            _ => CidmExternalUserResolution.Deny(NotPermittedReasons.PathFor())
        };
    }

    private static Dictionary<string, string> BuildClaims(ResolveExternalUserResponse response)
    {
        var claims = new Dictionary<string, string>
        {
            [ExternalUserClaimTypes.DisplayName] = response.DisplayName,
            [ExternalUserClaimTypes.ResolvedRoles] = string.Join(',', response.Roles),
            [ExternalUserClaimTypes.UserType] = $"{response.UserType}",
            [ExternalUserClaimTypes.LabCount] = response.Labs.Count.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(response.LabCode))
        {
            claims[ExternalUserClaimTypes.LabCode] = response.LabCode;
        }

        if (response.CanOrderOnline)
        {
            claims[ExternalUserClaimTypes.CanOrderOnline] = "true";
        }

        if (response.CanEditDetails)
        {
            claims[ExternalUserClaimTypes.CanEditDetails] = "true";
        }

        AddId(claims, ExternalUserClaimTypes.ParticipantId, response.ParticipantId);
        AddId(claims, ExternalUserClaimTypes.ViewerId, response.ViewerId);
        AddId(claims, ExternalUserClaimTypes.TestConsultantId, response.TestConsultantId);
        AddId(claims, ExternalUserClaimTypes.LabUserId, response.LabUserId);

        return claims;
    }

    private static void AddId(Dictionary<string, string> claims, string claimType, Guid? id)
    {
        if (id is { } value)
        {
            claims[claimType] = value.ToString();
        }
    }

    // CIDM's display-name claim set isn't fully confirmed for PT-LIMS's registration, so this
    // tries the same conventions CDC's resolver tries (firstName/lastName), then falls back to
    // more standard OIDC claim names, before giving up to an empty string. Capped to what PTL.Api accepts.
    private static string ResolveDisplayName(ClaimsPrincipal principal)
    {
        var firstName = principal.FindFirst("firstName")?.Value ?? principal.FindFirst(ClaimTypes.GivenName)?.Value;
        var lastName = principal.FindFirst("lastName")?.Value ?? principal.FindFirst(ClaimTypes.Surname)?.Value;
        var fullName = string.Join(' ', new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var displayName = !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : principal.FindFirst("name")?.Value ?? principal.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

        return displayName.Length > ResolveExternalUserRequest.MaxDisplayNameLength
            ? displayName[..ResolveExternalUserRequest.MaxDisplayNameLength]
            : displayName;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to resolve external user (SsoIdExt={SsoIdExt}) against PTL.Api")]
    private partial void LogExternalUserResolutionFailed(Guid ssoIdExt, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "CIDM resolve denied: no parseable {Claim} claim present")]
    private partial void LogMissingContactIdClaim(string claim);
}
