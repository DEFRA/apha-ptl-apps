using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Authorization;

// Expands ExternalUserResolver's ResolvedRoles claim into one ClaimTypes.Role claim per role, so
// ASP.NET Core [Authorize(Policy=...)] can use them - mirrors
// PTL.InternalWeb.Authorization.PtlRoleClaimsTransformation. No normalization table is needed here
// (unlike PtlRoles.ToClaimValue()) since ExternalRoleNames' values are already valid policy/role
// names as-is. Runs on every request, so it must be idempotent.
public sealed class ExternalRoleClaimsTransformation : IClaimsTransformation
{
    private const string RoleIdentityAuthenticationType = "ExternalRoles";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var resolved = principal.FindFirst(ExternalUserClaimTypes.ResolvedRoles)?.Value;
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(resolved))
        {
            return Task.FromResult(principal);
        }

        var roleClaims = resolved
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(role => !principal.HasClaim(ClaimTypes.Role, role))
            .Select(role => new Claim(ClaimTypes.Role, role))
            .ToList();

        if (roleClaims.Count == 0)
        {
            return Task.FromResult(principal);
        }

        var transformed = principal.Clone();
        transformed.AddIdentity(new ClaimsIdentity(roleClaims, RoleIdentityAuthenticationType, ClaimTypes.Name, ClaimTypes.Role));
        return Task.FromResult(transformed);
    }
}
