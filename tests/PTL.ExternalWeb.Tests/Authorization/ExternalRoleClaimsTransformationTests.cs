using System.Security.Claims;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Tests.Authorization;

// The external roles resolved by ExternalUserService at sign-in (resolvedRoles claim) become
// ClaimTypes.Role claims so [Authorize(Policy=...)]/User.IsInRole() can use them.
public class ExternalRoleClaimsTransformationTests
{
    private static readonly ExternalRoleClaimsTransformation Transformation = new();

    private static ClaimsPrincipal Principal(string? resolvedRoles, bool authenticated = true)
    {
        var claims = new List<Claim>();
        if (resolvedRoles is not null)
        {
            claims.Add(new Claim(ExternalUserClaimTypes.ResolvedRoles, resolvedRoles));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null));
    }

    private static IEnumerable<string> RoleClaims(ClaimsPrincipal principal) =>
        principal.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);

    [Fact]
    public async Task TransformAsync_ExpandsEachResolvedRoleIntoARoleClaim()
    {
        var result = await Transformation.TransformAsync(Principal("Participant,Test Consultant"));

        Assert.Equal(["Participant", "Test Consultant"], RoleClaims(result).Order());
        Assert.True(result.IsInRole(ExternalRoleNames.Participant));
        Assert.False(result.IsInRole(ExternalRoleNames.Viewer));
    }

    [Fact]
    public async Task TransformAsync_RunTwice_DoesNotDuplicateRoleClaims()
    {
        var once = await Transformation.TransformAsync(Principal("Participant,Viewer"));

        var twice = await Transformation.TransformAsync(once);

        Assert.Equal(2, RoleClaims(twice).Count());
        Assert.Same(once, twice);
    }

    [Fact]
    public async Task TransformAsync_UnauthenticatedPrincipal_ReturnsUnchanged()
    {
        var principal = Principal("Participant", authenticated: false);

        var result = await Transformation.TransformAsync(principal);

        Assert.Empty(RoleClaims(result));
        Assert.Same(principal, result);
    }

    [Fact]
    public async Task TransformAsync_NoResolvedRolesClaim_ReturnsUnchanged()
    {
        var principal = Principal(resolvedRoles: null);

        var result = await Transformation.TransformAsync(principal);

        Assert.Empty(RoleClaims(result));
        Assert.Same(principal, result);
    }

    [Fact]
    public async Task TransformAsync_NullPrincipal_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Transformation.TransformAsync(null!));
}
