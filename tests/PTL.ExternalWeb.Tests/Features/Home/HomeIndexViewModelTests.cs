using PTL.ExternalWeb.Features.Home;

namespace PTL.ExternalWeb.Tests.Features.Home;

public class HomeIndexViewModelTests
{
    [Fact]
    public void RoleFlags_AreFalse_WhenNoRolesResolved()
    {
        var model = new HomeIndexViewModel("Jane Doe", []);

        Assert.False(model.IsParticipant);
        Assert.False(model.IsViewer);
        Assert.False(model.IsTestConsultant);
    }

    [Fact]
    public void RoleFlags_ReflectResolvedRoles_CaseInsensitively()
    {
        var model = new HomeIndexViewModel("Jane Doe", ["participant", "TEST CONSULTANT"]);

        Assert.True(model.IsParticipant);
        Assert.False(model.IsViewer);
        Assert.True(model.IsTestConsultant);
    }

    [Fact]
    public void RoleFlags_SupportMultipleSimultaneousRoles()
    {
        var model = new HomeIndexViewModel("Jane Doe", ["Participant", "Viewer", "Test Consultant"]);

        Assert.True(model.IsParticipant);
        Assert.True(model.IsViewer);
        Assert.True(model.IsTestConsultant);
    }
}
