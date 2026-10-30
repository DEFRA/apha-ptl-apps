using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Distribution;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Distribution;

// Previous/Results behaviour is covered end-to-end by Integration/DistributionPreviousAndResultsRouteSmokeTests.cs
// (they need a real HTTP pipeline for User.GetParticipantId() and the rendered Razor views).
public class DistributionControllerTests
{
    [Fact]
    public void Current_ReturnsComingSoonViewForEnterResults()
    {
        var controller = new DistributionController(new FakeDistributionApiClient());

        var result = controller.Current();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("_ComingSoonBody", viewResult.ViewName);
        Assert.Equal("Enter results", viewResult.Model);
    }
}
