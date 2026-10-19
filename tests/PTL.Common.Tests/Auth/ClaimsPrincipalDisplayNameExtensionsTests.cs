using System.Security.Claims;
using PTL.Common.Auth;

namespace PTL.Common.Tests.Auth;

public class ClaimsPrincipalDisplayNameExtensionsTests
{
    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void NullPrincipal_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ClaimsPrincipalDisplayNameExtensions.GetDisplayName(null!));

    [Fact]
    public void NameClaim_IsPreferredOverEveryEntraFallback()
    {
        var principal = Principal(("name", "John Smith"), ("given_name", "J"), ("family_name", "S"), ("preferred_username", "john@org.gov.uk"));

        Assert.Equal("John Smith", principal.GetDisplayName());
    }

    [Fact]
    public void MappedNameClaimType_IsUsed() =>
        Assert.Equal("John Smith", Principal((ClaimTypes.Name, "John Smith")).GetDisplayName());

    [Theory]
    [InlineData("given_name", "family_name")]
    [InlineData(ClaimTypes.GivenName, ClaimTypes.Surname)]
    public void WithoutName_GivenAndFamilyNamesAreJoined(string givenType, string familyType) =>
        Assert.Equal("John Smith", Principal((givenType, "John"), (familyType, "Smith")).GetDisplayName());

    [Fact]
    public void OnlyGivenName_IsReturnedAlone() =>
        Assert.Equal("John", Principal(("given_name", "John")).GetDisplayName());

    [Theory]
    [InlineData("preferred_username")]
    [InlineData(ClaimTypes.Upn)]
    [InlineData("upn")]
    [InlineData(ClaimTypes.Email)]
    [InlineData("email")]
    public void WithoutAnyName_SignInIdentifierIsUsed(string claimType) =>
        Assert.Equal("john.smith@organisation.gov.uk", Principal((claimType, "john.smith@organisation.gov.uk")).GetDisplayName());

    [Fact]
    public void BlankValues_AreSkipped() =>
        Assert.Equal("john@org.gov.uk", Principal(("name", "   "), ("preferred_username", "john@org.gov.uk")).GetDisplayName());

    [Fact]
    public void PreferredClaimType_WinsOverNameClaim() =>
        Assert.Equal("Jane Doe", Principal(("name", "John Smith"), ("displayName", "Jane Doe")).GetDisplayName("displayName"));

    [Fact]
    public void MissingPreferredClaimType_FallsThroughToNameClaim() =>
        Assert.Equal("John Smith", Principal(("name", "John Smith")).GetDisplayName("displayName"));

    [Fact]
    public void LastResortClaimType_IsUsedOnlyWhenNothingElseExists()
    {
        Assert.Equal("Test Admin", Principal(("fullName", "Test Admin")).GetDisplayName(lastResortClaimType: "fullName"));
        Assert.Equal("john@org.gov.uk", Principal(("fullName", "Test Admin"), ("email", "john@org.gov.uk")).GetDisplayName(lastResortClaimType: "fullName"));
    }

    [Fact]
    public void NoClaims_ReturnsNull() =>
        Assert.Null(Principal().GetDisplayName("displayName", "fullName"));
}
