using System.Security.Claims;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Features.Account;

namespace PTL.InternalWeb.Tests.Authorization;

// The PTL roles resolved from the PTL database at sign-in (ResolvedRoles claim) become role claims; Entra ID
// roles/groups play no part.
public class PtlRoleClaimsTransformationTests
{
    private static readonly PtlRoleClaimsTransformation Transformation = new();

    private static ClaimsPrincipal Principal(string? resolvedRoles, bool authenticated = true)
    {
        var claims = new List<Claim>();
        if (resolvedRoles is not null)
        {
            claims.Add(new Claim(InternalUserClaimTypes.ResolvedRoles, resolvedRoles));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null));
    }

    private static IEnumerable<string> RoleClaims(ClaimsPrincipal principal) =>
        principal.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);

    [Fact]
    public async Task TransformAsync_ExpandsEachPtlRoleIntoARoleClaim()
    {
        var result = await Transformation.TransformAsync(Principal("Admin,Contracts Admin,Results Sign-off"));

        Assert.Equal(["Admin", "ContractsAdmin", "ResultsSignOff"], RoleClaims(result).Order());
        Assert.True(result.IsInRole(PtlRoles.ContractsAdmin));
        Assert.False(result.IsInRole(PtlRoles.SchemeAdmin));
    }

    [Theory]
    [InlineData("Admin", PtlRoles.Admin)]
    [InlineData("Contracts Admin", PtlRoles.ContractsAdmin)]
    [InlineData("Scheme Admin", PtlRoles.SchemeAdmin)]
    [InlineData("Internal User", PtlRoles.InternalUser)]
    [InlineData("Scheduling", PtlRoles.Scheduling)]
    [InlineData("Distributions", PtlRoles.Distributions)]
    [InlineData("Test Consultant", PtlRoles.TestConsultant)]
    [InlineData("Assessor", PtlRoles.Assessor)]
    [InlineData("Results Sign-off", PtlRoles.ResultsSignOff)]
    [InlineData("Generate Invoices", PtlRoles.GenerateInvoices)]
    [InlineData("Override Schemes", PtlRoles.OverrideSchemes)]
    [InlineData("Support", PtlRoles.Support)]
    public async Task TransformAsync_MapsEveryLegacyRoleNameToItsClaimValue(string ptlRole, string claimValue)
    {
        var result = await Transformation.TransformAsync(Principal(ptlRole));

        Assert.Equal([claimValue], RoleClaims(result));
    }

    [Fact]
    public async Task TransformAsync_RunTwice_DoesNotDuplicateRoleClaims()
    {
        var once = await Transformation.TransformAsync(Principal("Admin,Scheduling"));

        var twice = await Transformation.TransformAsync(once);

        Assert.Equal(2, RoleClaims(twice).Count());
        Assert.Same(once, twice);
    }

    [Fact]
    public async Task TransformAsync_IgnoresUnknownBlankAndRepeatedNames_AndMatchesCaseInsensitively()
    {
        var result = await Transformation.TransformAsync(Principal("Bogus, ,  contracts admin ,Admin,ADMIN"));

        Assert.Equal(["Admin", "ContractsAdmin"], RoleClaims(result).Order());
    }

    [Fact]
    public async Task TransformAsync_WithoutResolvedRoles_ReturnsThePrincipalUnchanged()
    {
        var principal = Principal(null);

        var result = await Transformation.TransformAsync(principal);

        Assert.Same(principal, result);
        Assert.Empty(RoleClaims(result));
    }

    [Fact]
    public async Task TransformAsync_UnknownRolesOnly_ReturnsThePrincipalUnchanged()
    {
        var principal = Principal("Bogus");

        var result = await Transformation.TransformAsync(principal);

        Assert.Same(principal, result);
    }

    [Fact]
    public async Task TransformAsync_UnauthenticatedPrincipal_GetsNoRoles()
    {
        var principal = Principal("Admin", authenticated: false);

        var result = await Transformation.TransformAsync(principal);

        Assert.Same(principal, result);
        Assert.Empty(RoleClaims(result));
    }

    [Fact]
    public async Task TransformAsync_NullPrincipal_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Transformation.TransformAsync(null!));

    [Fact]
    public void ToClaimValue_UnknownRole_ReturnsNull() =>
        Assert.Null(PtlRoles.ToClaimValue("Entra Group Object Id"));
}
