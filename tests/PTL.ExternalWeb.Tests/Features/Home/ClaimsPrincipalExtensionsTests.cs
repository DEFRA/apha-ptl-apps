using System.Security.Claims;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Tests.Features.Home;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetResolvedExternalRoles_ReturnsEmpty_WhenClaimMissing()
    {
        var principal = CreatePrincipal(resolvedRoles: null);

        var roles = principal.GetResolvedExternalRoles();

        Assert.Empty(roles);
    }

    [Fact]
    public void GetResolvedExternalRoles_SplitsAndTrimsCommaSeparatedValue()
    {
        var principal = CreatePrincipal(resolvedRoles: "Participant, Viewer ,Test Consultant");

        var roles = principal.GetResolvedExternalRoles();

        Assert.Equal(["Participant", "Viewer", "Test Consultant"], roles);
    }

    [Fact]
    public void GetResolvedExternalRoles_IgnoresEmptyEntries()
    {
        var principal = CreatePrincipal(resolvedRoles: "Participant,,Viewer");

        var roles = principal.GetResolvedExternalRoles();

        Assert.Equal(["Participant", "Viewer"], roles);
    }

    [Theory]
    [InlineData("Participant", ExternalRoleNames.Participant, true)]
    [InlineData("participant", ExternalRoleNames.Participant, true)]
    [InlineData("Viewer", ExternalRoleNames.Participant, false)]
    public void HasResolvedExternalRole_IsCaseInsensitive(string resolvedRoles, string roleToCheck, bool expected)
    {
        var principal = CreatePrincipal(resolvedRoles);

        Assert.Equal(expected, principal.HasResolvedExternalRole(roleToCheck));
    }

    private static ClaimsPrincipal CreatePrincipal(string? resolvedRoles)
    {
        var claims = new List<Claim>();
        if (resolvedRoles is not null)
        {
            claims.Add(new Claim(ExternalUserClaimTypes.ResolvedRoles, resolvedRoles));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
