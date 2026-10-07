using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Features.Home;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Home;

public class HomeControllerTests
{
    private static HomeController CreateController(params Claim[] claims) =>
        new(new FakeApiClient())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };

    [Fact]
    public void Index_DisplayNameAndResolvedRolesClaimsPresent_ShowsBoth()
    {
        var controller = CreateController(
            new Claim(ExternalUserClaimTypes.DisplayName, "Jane Doe"),
            new Claim(ExternalUserClaimTypes.ResolvedRoles, "Viewer,Participant"));

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
        Assert.Equal("Jane Doe", model.DisplayName);
        Assert.Equal(["Viewer", "Participant"], model.Roles);
    }

    [Fact]
    public void Index_NoResolvedRolesClaim_ShowsNoRoles()
    {
        var controller = CreateController(new Claim(ExternalUserClaimTypes.DisplayName, "Jane Doe"));

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
        Assert.Empty(model.Roles);
    }

    [Fact]
    public void Index_NoDisplayNameClaim_FallsBackToIdentityName()
    {
        var controller = CreateController(new Claim(ClaimTypes.Name, "fallback-name"));

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
        Assert.Equal("fallback-name", model.DisplayName);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        var controller = new HomeController(new FakeApiClient());

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task ApiStatus_ReturnsJsonFromApiClient()
    {
        var expected = new ApiHealthResponse("Healthy", 42, DateTime.UtcNow);
        var controller = new HomeController(new FakeApiClient(expected));

        var result = await controller.ApiStatus(CancellationToken.None);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.Equal(expected, jsonResult.Value);
    }

    [Fact]
    public void Error_ReturnsViewWithRequestId()
    {
        var controller = new HomeController(new FakeApiClient())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = controller.Error();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.True(model.ShowRequestId);
    }
}
