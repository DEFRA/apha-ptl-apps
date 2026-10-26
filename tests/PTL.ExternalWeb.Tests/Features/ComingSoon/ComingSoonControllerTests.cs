using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Features.ComingSoon;

namespace PTL.ExternalWeb.Tests.Features.ComingSoon;

public class ComingSoonControllerTests
{
    private static ComingSoonController CreateController(params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return new ComingSoonController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };
    }

    [Fact]
    public void Index_GivenTitle_ReturnsViewWithMatchingViewModel()
    {
        var controller = new ComingSoonController();

        var result = controller.Index("View schemes");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ComingSoonViewModel>(viewResult.Model);
        Assert.Equal("View schemes", model.Title);
    }

    [Fact]
    public void Index_NoRequiredRole_ReturnsViewRegardlessOfUserRoles()
    {
        var controller = CreateController("Viewer");

        var result = controller.Index("View schemes", requiredRole: null);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Index_UserHasRequiredRole_ReturnsView()
    {
        var controller = CreateController("Participant");

        var result = controller.Index("Create order", requiredRole: "Participant");

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Index_UserLacksRequiredRole_ReturnsForbid()
    {
        var controller = CreateController("Participant");

        var result = controller.Index("View reports", requiredRole: "Viewer");

        Assert.IsType<ForbidResult>(result);
    }
}
