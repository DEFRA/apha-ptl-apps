using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Order;

namespace PTL.ExternalWeb.Tests.Features.Order;

public class OrderControllerTests
{
    [Fact]
    public void Index_ReturnsComingSoonViewForCreateOrder()
    {
        var controller = new OrderController();

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("_ComingSoonBody", viewResult.ViewName);
        Assert.Equal("Create order", viewResult.Model);
    }
}
