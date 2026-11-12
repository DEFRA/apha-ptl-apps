using System.Security.Claims;
using PTL.Contracts.ExternalUser;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Features.Lab;

namespace PTL.ExternalWeb.Tests.Features.Lab;

public class LabSessionTests
{
    private static readonly Guid OldLab = Guid.NewGuid();
    private static readonly Guid NewLab = Guid.NewGuid();

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(
        [
            new Claim("contactId", "c"),
            new Claim(ExternalUserClaimTypes.UserType, "Participant"),
            new Claim(ExternalUserClaimTypes.ParticipantId, OldLab.ToString()),
            new Claim(ExternalUserClaimTypes.LabCode, "OLD"),
            new Claim(ExternalUserClaimTypes.CanOrderOnline, "true"),
            new Claim(ExternalUserClaimTypes.LabCount, "3"),
            .. claims
        ], "Test", ClaimTypes.Name, ClaimTypes.Role));

    [Fact]
    public void WithLab_AddsTheEditDetailsClaimOnlyWhenTheNewLabMayEdit()
    {
        var staleClaim = new Claim(ExternalUserClaimTypes.CanEditDetails, "true");

        var mayEdit = LabSession.WithLab(Principal(), new ExternalLabContext(NewLab, "NEW", "New Lab", CanOrderOnline: false, CanEditDetails: true), labCount: 2);
        var mayNotEdit = LabSession.WithLab(Principal(staleClaim), new ExternalLabContext(NewLab, "NEW", "New Lab", CanOrderOnline: false, CanEditDetails: false), labCount: 2);

        Assert.True(mayEdit.CanEditDetails());
        Assert.False(mayNotEdit.CanEditDetails());
    }

    [Fact]
    public void WithLab_ReplacesEveryLabClaimAndKeepsTheRest()
    {
        var result = LabSession.WithLab(Principal(new Claim("other", "kept")), new ExternalLabContext(NewLab, "NEW", "New Lab", CanOrderOnline: false), labCount: 2);

        Assert.Equal(NewLab, result.GetParticipantId());
        Assert.Equal("NEW", result.FindFirst(ExternalUserClaimTypes.LabCode)?.Value);
        Assert.Equal(2, result.GetLabCount());
        Assert.False(result.CanOrderOnline());
        Assert.Equal("c", result.FindFirst("contactId")?.Value);
        Assert.Equal("Participant", result.FindFirst(ExternalUserClaimTypes.UserType)?.Value);
        Assert.Equal("kept", result.FindFirst("other")?.Value);
        Assert.Single(result.FindAll(ExternalUserClaimTypes.ParticipantId));
        Assert.Single(result.FindAll(ExternalUserClaimTypes.LabCode));
        Assert.Single(result.FindAll(ExternalUserClaimTypes.LabCount));
    }

    [Fact]
    public void WithLab_LabThatCanOrderOnline_AddsTheClaim_AndNoneWhenNotAndTheOldOneIsDropped()
    {
        var principal = Principal();

        Assert.True(LabSession.WithLab(principal, new ExternalLabContext(NewLab, "NEW", "n", true), 2).CanOrderOnline());
        Assert.False(LabSession.WithLab(principal, new ExternalLabContext(NewLab, "NEW", "n", false), 2).CanOrderOnline());
    }

    [Fact]
    public void WithLab_DoesNotChangeTheOriginalPrincipal()
    {
        var principal = Principal();

        LabSession.WithLab(principal, new ExternalLabContext(NewLab, "NEW", "n", false), 2);

        Assert.Equal(OldLab, principal.GetParticipantId());
    }

    [Fact]
    public void WithLab_CopiesOnlyTheSessionsOwnIdentity_NotTheRoleIdentityAddedPerRequest()
    {
        var principal = Principal();
        principal.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Participant")], "ExternalRoles"));

        var result = LabSession.WithLab(principal, new ExternalLabContext(NewLab, "NEW", "n", false), 2);

        Assert.Single(result.Identities);
        Assert.DoesNotContain(result.Claims, claim => claim.Type == ClaimTypes.Role);
    }

    [Fact]
    public void WithLab_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => LabSession.WithLab(null!, new ExternalLabContext(NewLab, "n", "n", false), 1));
        Assert.Throws<ArgumentNullException>(() => LabSession.WithLab(Principal(), null!, 1));
    }
}
