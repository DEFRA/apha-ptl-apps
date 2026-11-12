using System.Globalization;
using System.Security.Claims;
using PTL.Contracts.ExternalUser;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Features.Lab;

/// <summary>Puts the chosen lab on the signed-in user's session identity.</summary>
public static class LabSession
{
    private static readonly string[] LabScopedClaimTypes =
    [
        ExternalUserClaimTypes.ParticipantId,
        ExternalUserClaimTypes.LabCode,
        ExternalUserClaimTypes.CanOrderOnline,
        ExternalUserClaimTypes.CanEditDetails,
        ExternalUserClaimTypes.LabCount
    ];

    /// <summary>
    /// A copy of the session identity acting for <paramref name="lab"/>: the lab claims are replaced and everything else is kept.
    /// Only the session's own (first) identity is copied, never the role identity added per request by the claims transformation.
    /// </summary>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="lab">The chosen lab, as PTL.Api confirmed it for the user.</param>
    /// <param name="labCount">How many active labs the user has now, so "Switch lab" is offered only while there is a choice.</param>
    public static ClaimsPrincipal WithLab(ClaimsPrincipal principal, ExternalLabContext lab, int labCount)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(lab);

        var identity = new ClaimsIdentity(principal.Identities.First());
        foreach (var claim in identity.Claims.Where(claim => LabScopedClaimTypes.Contains(claim.Type)).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(ExternalUserClaimTypes.ParticipantId, lab.ParticipantId.ToString()));
        identity.AddClaim(new Claim(ExternalUserClaimTypes.LabCode, lab.LabCode));
        identity.AddClaim(new Claim(ExternalUserClaimTypes.LabCount, labCount.ToString(CultureInfo.InvariantCulture)));
        if (lab.CanOrderOnline)
        {
            identity.AddClaim(new Claim(ExternalUserClaimTypes.CanOrderOnline, "true"));
        }

        if (lab.CanEditDetails)
        {
            identity.AddClaim(new Claim(ExternalUserClaimTypes.CanEditDetails, "true"));
        }

        return new ClaimsPrincipal(identity);
    }
}
