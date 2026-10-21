using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using PTL.InternalWeb.Features.Account;

namespace PTL.InternalWeb.Authorization;

// Expands the PTL roles InternalUserResolver stored in the ResolvedRoles claim at sign-in into one
// ClaimTypes.Role claim per role, so ASP.NET Core authorization policies can use them. Runs on every
// request, so it must be idempotent. Unrecognised names are ignored; nothing is logged about claims.
public sealed class PtlRoleClaimsTransformation : IClaimsTransformation
{
    private const string RoleIdentityAuthenticationType = "PtlRoles";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var resolved = principal.FindFirst(InternalUserClaimTypes.ResolvedRoles)?.Value;
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(resolved))
        {
            return Task.FromResult(principal);
        }

        var roleClaims = resolved
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(PtlRoles.ToClaimValue)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
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
