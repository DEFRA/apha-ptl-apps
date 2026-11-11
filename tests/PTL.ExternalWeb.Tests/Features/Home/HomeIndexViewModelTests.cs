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

    [Fact]
    public void SubmittedOrderMessage_NamesTheYearOnlyForTheSubmittedYear()
    {
        var model = new HomeIndexViewModel("Jane Doe", [], null, false, true, "2026/27", "2027/28");

        Assert.Equal("Order has been submitted for 2026/27.", model.SubmittedOrderMessage("CurrentYear"));
        Assert.Null(model.SubmittedOrderMessage("NextYear"));
        Assert.Null(model.SubmittedOrderMessage("Current"));
    }

    [Fact]
    public void SubmittedOrderMessage_NextYear_UsesTheNextYearLabel()
    {
        var model = new HomeIndexViewModel("Jane Doe", [], null, true, false, "2026/27", "2027/28");

        Assert.Equal("Order has been submitted for 2027/28.", model.SubmittedOrderMessage("NextYear"));
        Assert.Null(model.SubmittedOrderMessage("CurrentYear"));
    }

    [Fact]
    public void SubmittedOrderMessage_WithoutAYearLabel_FallsBackToWords()
    {
        var model = new HomeIndexViewModel("Jane Doe", [], null, false, false);

        Assert.Equal("Order has been submitted for the current year.", model.SubmittedOrderMessage("CurrentYear"));
        Assert.Equal("Order has been submitted for the next year.", model.SubmittedOrderMessage("NextYear"));
    }
}
