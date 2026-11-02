using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Tabulation;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Tabulation;

// Index/Report/AssessmentResults/Attachment behaviour is covered end-to-end by
// Integration/TabulationRouteSmokeTests.cs (they need a real HTTP pipeline for
// User.GetParticipantId()/GetViewerId()/GetTestConsultantId() and the rendered Razor views).
public class TabulationControllerTests
{
    [Fact]
    public void GeneratePdf_ReturnsComingSoonViewUntilReportingBuildsIt()
    {
        var controller = new TabulationController(new FakeDistributionApiClient());

        var result = controller.GeneratePdf(Guid.NewGuid());

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("_ComingSoonBody", viewResult.ViewName);
        Assert.Equal("Generate PDF", viewResult.Model);
    }
}
