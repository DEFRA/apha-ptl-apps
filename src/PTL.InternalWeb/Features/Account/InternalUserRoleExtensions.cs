using System.Security.Claims;

namespace PTL.InternalWeb.Features.Account;

/// <summary>tblRoles role names that gate individual features, as legacy checked with IsInRole.</summary>
public static class InternalRoleNames
{
    public const string OverrideSchemes = "Override Schemes";
}

public static class InternalUserRoleExtensions
{
    /// <summary>
    /// True when the principal's <see cref="InternalUserClaimTypes.ResolvedRoles"/> claim lists the role
    /// (legacy <c>User.IsInRole</c>; names are compared case-insensitively).
    /// </summary>
    public static bool HasInternalRole(this ClaimsPrincipal? principal, string role)
    {
        var resolved = principal?.FindFirst(InternalUserClaimTypes.ResolvedRoles)?.Value;
        return !string.IsNullOrWhiteSpace(resolved)
            && resolved.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
