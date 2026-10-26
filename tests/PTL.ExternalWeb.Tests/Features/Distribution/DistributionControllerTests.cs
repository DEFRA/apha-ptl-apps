using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Distribution;

namespace PTL.ExternalWeb.Tests.Features.Distribution;

public class DistributionControllerTests
{
    [Fact]
    public void Current_ReturnsComingSoonViewForEnterResults()
    {
        var controller = new DistributionController();

        var result = controller.Current();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("_ComingSoonBody", viewResult.ViewName);
        Assert.Equal("Enter results", viewResult.Model);
    }

    [Fact]
    public void Previous_ReturnsComingSoonViewForViewResults()
    {
        var controller = new DistributionController();

        var result = controller.Previous();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("_ComingSoonBody", viewResult.ViewName);
        Assert.Equal("View results", viewResult.Model);
    }
}
