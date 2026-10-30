using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Polly;
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
/// <param name="logger">Logs resolution failures so a sign-in landing on /Home/Error is diagnosable.</param>
public sealed partial class ExternalUserResolver(IExternalUserApiClient externalUserApiClient, ILogger<ExternalUserResolver> logger) : ICidmExternalUserResolver
{
    /// <inheritdoc />
    public async Task<CidmExternalUserResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // contactId is CIDM's stable identifier for the person, independent of which
        // organisation/relationship they're currently acting under.
        if (!Guid.TryParse(principal.FindFirst(CidmClaimTypes.ContactId)?.Value, out var ssoIdExt))
        {
            LogMissingContactIdClaim(CidmClaimTypes.ContactId);
            return CidmExternalUserResolution.Deny("/Account/NotPermitted");
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value ?? string.Empty;
        var displayName = ResolveDisplayName(principal);

        // CidmOpenIdConnectEvents.TokenValidated already added a standard ClaimTypes.Role claim for
        // each raw CIDM role ("Participant", "Viewer", "Test Consultant", ...) before calling this
        // resolver - a user can hold more than one simultaneously.
        var cidmRoles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct().ToList();

        ResolveExternalUserResponse response;
        try
        {
            response = await externalUserApiClient.ResolveAsync(
                new ResolveExternalUserRequest(ssoIdExt, email, displayName, cidmRoles),
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

        // No CIDM role resolved to an existing record - nothing in PTL this person is permitted to
        // use, so deny entirely rather than signing them into an app with no accessible data. The
        // "inactive" reason is surfaced distinctly so NotPermitted can show clearer guidance when a
        // record exists but is marked inactive, rather than the generic "no access at all" message.
        if (response.Roles.Count == 0)
        {
            var reason = response.HasInactiveMatch ? "?reason=inactive" : string.Empty;
            return CidmExternalUserResolution.Deny($"/Account/NotPermitted{reason}");
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

        if (response.ParticipantId is { } participantId)
        {
            claims[ExternalUserClaimTypes.ParticipantId] = participantId.ToString();
        }

        if (response.ViewerId is { } viewerId)
        {
            claims[ExternalUserClaimTypes.ViewerId] = viewerId.ToString();
        }

        if (response.TestConsultantId is { } testConsultantId)
        {
            claims[ExternalUserClaimTypes.TestConsultantId] = testConsultantId.ToString();
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to resolve external user (SsoIdExt={SsoIdExt}) against PTL.Api")]
    private partial void LogExternalUserResolutionFailed(Guid ssoIdExt, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "CIDM resolve denied: no parseable {Claim} claim present")]
    private partial void LogMissingContactIdClaim(string claim);
}
