using System.Security.Claims;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Features.TestConsultant;

namespace PTL.InternalWeb.Tests.Features.TestConsultant;

public class TestConsultantPolicyTests
{
    private static ClaimsPrincipal User(string? roles, bool authenticated = true)
    {
        var claims = roles is null ? [] : new[] { new Claim(InternalUserClaimTypes.ResolvedRoles, roles) };
        return new ClaimsPrincipal(authenticated ? new ClaimsIdentity(claims, "Test") : new ClaimsIdentity(claims));
    }

    [Theory]
    [InlineData("Test Consultant")]
    [InlineData("Admin,Test Consultant")]
    [InlineData("Admin, Test Consultant ,Scheduling")]
    [InlineData("test consultant")]
    public void IsSatisfiedBy_UserWithTheRole_IsAllowed(string roles) => Assert.True(TestConsultantPolicy.IsSatisfiedBy(User(roles)));

    [Theory]
    [InlineData("Admin")]
    [InlineData("Admin,Scheduling")]
    [InlineData("Test Consultants")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSatisfiedBy_UserWithoutTheRole_IsRefused(string? roles) => Assert.False(TestConsultantPolicy.IsSatisfiedBy(User(roles)));

    [Fact]
    public void IsSatisfiedBy_UnauthenticatedUserWithTheRoleClaim_IsRefused() =>
        Assert.False(TestConsultantPolicy.IsSatisfiedBy(User("Test Consultant", authenticated: false)));

    [Fact]
    public void IsSatisfiedBy_NullUser_Throws() => Assert.Throws<ArgumentNullException>(() => TestConsultantPolicy.IsSatisfiedBy(null!));

    [Fact]
    public void RoleName_MatchesTheDatabaseRole() => Assert.Equal("Test Consultant", TestConsultantPolicy.RoleName);
}
