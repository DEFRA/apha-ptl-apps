using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Tabulation;

namespace PTL.ExternalWeb.Tests.Features.Tabulation;

public class TabulationControllerTests
{
    [Fact]
    public void Index_ReturnsComingSoonViewForViewReports()
    {
        var controller = new TabulationController();

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("_ComingSoonBody", viewResult.ViewName);
        Assert.Equal("View reports", viewResult.Model);
    }
}
