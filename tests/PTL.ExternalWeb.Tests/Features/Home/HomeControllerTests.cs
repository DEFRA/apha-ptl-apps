using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Home;
using PTL.ExternalWeb.Models;

namespace PTL.ExternalWeb.Tests.Features.Home;

public class HomeControllerTests
{
    [Fact]
    public void Index_ReturnsView()
    {
        var controller = new HomeController();

        var result = controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        var controller = new HomeController();

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Error_ReturnsViewWithRequestId()
    {
        var controller = new HomeController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = controller.Error();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.True(model.ShowRequestId);
    }
}
