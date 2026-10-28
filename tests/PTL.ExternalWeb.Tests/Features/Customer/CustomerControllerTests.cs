using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.Customer;

namespace PTL.ExternalWeb.Tests.Features.Customer;

public class CustomerControllerTests
{
    [Fact]
    public void Index_ReturnsComingSoonViewForEditCustomerDetails()
    {
        var controller = new CustomerController();

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("_ComingSoonBody", viewResult.ViewName);
        Assert.Equal("Edit customer details", viewResult.Model);
    }
}
