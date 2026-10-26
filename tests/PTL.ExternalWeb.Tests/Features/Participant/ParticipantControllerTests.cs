using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Participant;

namespace PTL.ExternalWeb.Tests.Features.Participant;

public class ParticipantControllerTests
{
    [Fact]
    public void Index_ReturnsComingSoonViewForEditParticipantDetails()
    {
        var controller = new ParticipantController();

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("_ComingSoonBody", viewResult.ViewName);
        Assert.Equal("Edit participant details", viewResult.Model);
    }
}
