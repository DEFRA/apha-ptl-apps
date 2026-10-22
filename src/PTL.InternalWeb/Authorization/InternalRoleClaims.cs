using System.Security.Claims;
using PTL.InternalWeb.Features.Account;

namespace PTL.InternalWeb.Authorization;

// PT-LIMS's own role names (tblRoles) are resolved into one comma-separated ResolvedRoles claim at
// sign-in (InternalUserResolver). Shared by every role-gated policy.
public static class InternalRoleClaims
{
    public static bool HasRole(ClaimsPrincipal user, string roleName)
    {
        ArgumentNullException.ThrowIfNull(user);

        var roles = user.FindFirst(InternalUserClaimTypes.ResolvedRoles)?.Value;
        return user.Identity?.IsAuthenticated == true
            && !string.IsNullOrEmpty(roles)
            && roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }
}
