using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.ComingSoon;

namespace PTL.ExternalWeb.Tests.Features.ComingSoon;

public class ComingSoonControllerTests
{
    [Fact]
    public void Index_GivenTitle_ReturnsViewWithMatchingViewModel()
    {
        var controller = new ComingSoonController();

        var result = controller.Index("View schemes");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ComingSoonViewModel>(viewResult.Model);
        Assert.Equal("View schemes", model.Title);
    }
}
