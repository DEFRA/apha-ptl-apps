using System.Security.Claims;
using PTL.Auth.Cidm.Claims;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Tests.Features.Account;

public class LabClaimsTests
{
    private static ClaimsPrincipal With(params Claim[] claims) => new(new ClaimsIdentity(claims, "Test"));

    [Fact]
    public void GetContactId_ReadsAGuid_AndIsNullForMissingOrMalformed()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, With(new Claim(CidmClaimTypes.ContactId, id.ToString())).GetContactId());
        Assert.Null(With(new Claim(CidmClaimTypes.ContactId, "not-a-guid")).GetContactId());
        Assert.Null(With().GetContactId());
    }

    [Theory]
    [InlineData("LabUser", true)]
    [InlineData("Participant", false)]
    [InlineData("Viewer", false)]
    [InlineData(null, false)]
    public void IsLabUser_ComesFromTheUserTypeNotTheRoles(string? userType, bool expected)
    {
        var principal = userType is null ? With() : With(new Claim(ExternalUserClaimTypes.UserType, userType));

        Assert.Equal(expected, principal.IsLabUser());
    }

    [Theory]
    [InlineData("0", 0, false)]
    [InlineData("1", 1, false)]
    [InlineData("2", 2, true)]
    [InlineData("12", 12, true)]
    [InlineData("-3", 0, false)]
    [InlineData("many", 0, false)]
    [InlineData("", 0, false)]
    [InlineData(null, 0, false)]
    public void LabCount_IsReadStrictly_AndOnlyMoreThanOneCanSwitch(string? claim, int count, bool canSwitch)
    {
        var principal = claim is null ? With() : With(new Claim(ExternalUserClaimTypes.LabCount, claim));

        Assert.Equal(count, principal.GetLabCount());
        Assert.Equal(canSwitch, principal.CanSwitchLab());
    }

    [Fact]
    public void MustSelectLab_OnlyWhenSeveralLabsAndNoneChosenYet()
    {
        var lab = new Claim(ExternalUserClaimTypes.ParticipantId, Guid.NewGuid().ToString());
        var many = new Claim(ExternalUserClaimTypes.LabCount, "2");
        var one = new Claim(ExternalUserClaimTypes.LabCount, "1");

        Assert.True(With(many).MustSelectLab());
        Assert.False(With(many, lab).MustSelectLab());
        Assert.False(With(one).MustSelectLab());
        Assert.False(With().MustSelectLab());
    }
}
