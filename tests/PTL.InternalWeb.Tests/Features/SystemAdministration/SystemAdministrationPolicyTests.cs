using System.Security.Claims;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Features.SystemAdministration;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

public class SystemAdministrationPolicyTests
{
    private static ClaimsPrincipal User(string? roles, bool authenticated = true)
    {
        var claims = roles is null ? [] : new[] { new Claim(InternalUserClaimTypes.ResolvedRoles, roles) };
        return new ClaimsPrincipal(authenticated ? new ClaimsIdentity(claims, "Test") : new ClaimsIdentity(claims));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Scheduling,Admin")]
    [InlineData("Scheduling, Admin ,Test Consultant")]
    [InlineData("admin")]
    public void IsSatisfiedBy_UserWithTheAdminRole_IsAllowed(string roles) => Assert.True(SystemAdministrationPolicy.IsSatisfiedBy(User(roles)));

    [Theory]
    [InlineData("Scheduling")]
    [InlineData("Test Consultant,Scheme Admin")]
    [InlineData("Administrator")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSatisfiedBy_UserWithoutTheAdminRole_IsRefused(string? roles) => Assert.False(SystemAdministrationPolicy.IsSatisfiedBy(User(roles)));

    [Fact]
    public void IsSatisfiedBy_UnauthenticatedUserWithTheAdminClaim_IsRefused() =>
        Assert.False(SystemAdministrationPolicy.IsSatisfiedBy(User("Admin", authenticated: false)));

    [Fact]
    public void AdminRoleName_MatchesTheDatabaseRole() => Assert.Equal("Admin", SystemAdministrationPolicy.AdminRoleName);
}
