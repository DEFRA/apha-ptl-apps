using System.Security.Claims;

namespace PTL.Common.Auth;

/// <summary>Resolves the "Signed in as" name shown in both web apps' headers from the session's existing claims.</summary>
public static class ClaimsPrincipalDisplayNameExtensions
{
    /// <summary>
    /// Returns the first available of: <paramref name="preferredClaimType"/>, the id_token "name"
    /// claim (Entra display name), given_name + family_name, preferred_username, upn, email, then
    /// <paramref name="lastResortClaimType"/>. Null when none is present.
    /// </summary>
    public static string? GetDisplayName(this ClaimsPrincipal principal, string? preferredClaimType = null, string? lastResortClaimType = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Both the raw OIDC names and the long URIs inbound JWT claim mapping can rewrite them to.
        var givenName = First(principal, "given_name", ClaimTypes.GivenName);
        var familyName = First(principal, "family_name", ClaimTypes.Surname);
        var fullName = string.Join(' ', new[] { givenName, familyName }.Where(part => part is not null));

        return First(principal, preferredClaimType)
            ?? First(principal, "name", ClaimTypes.Name)
            ?? (fullName.Length > 0 ? fullName : null)
            ?? First(principal, "preferred_username", ClaimTypes.Upn, "upn", ClaimTypes.Email, "email")
            ?? First(principal, lastResortClaimType);
    }

    private static string? First(ClaimsPrincipal principal, params string?[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            if (claimType is null)
            {
                continue;
            }

            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
