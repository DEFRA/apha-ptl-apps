using Microsoft.AspNetCore.Mvc;
using PTL.InternalWeb.Features.Menu;

namespace PTL.InternalWeb.Tests.Features.Menu;

public class MenuControllerTests
{
    [Fact]
    public void ManageContracts_ReturnsView()
    {
        var controller = new MenuController();

        var result = controller.ManageContracts();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void ManageSchemes_ReturnsView()
    {
        var controller = new MenuController();

        var result = controller.ManageSchemes();

        Assert.IsType<ViewResult>(result);
    }
}
