using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Account;

public class AccountControllerTests
{
    [Fact]
    public void Login_Get_ReturnsViewWithReturnUrl()
    {
        var controller = new AccountController();

        var result = controller.Login("/dashboard");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AccountViewModel>(viewResult.Model);
        Assert.Equal("/dashboard", model.ReturnUrl);
    }

    [Fact]
    public async Task Login_Post_WithNullModel_ReturnsViewWithNewModel()
    {
        var controller = new AccountController();

        var result = await controller.Login((AccountViewModel)null!);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<AccountViewModel>(viewResult.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Login_Post_WithMissingCredentials_ReturnsViewWithModelError()
    {
        var controller = new AccountController();
        var model = new AccountViewModel { Username = "", Password = "" };

        var result = await controller.Login(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Login_Post_WithValidCredentialsAndNoReturnUrl_RedirectsToHome()
    {
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContextWithFakeAuth() },
            Url = new FakeUrlHelper()
        };
        var model = new AccountViewModel { Username = "alice", Password = "secret" };

        var result = await controller.Login(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task Login_Post_WithValidCredentialsAndLocalReturnUrl_RedirectsToReturnUrl()
    {
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContextWithFakeAuth() },
            Url = new FakeUrlHelper()
        };
        var model = new AccountViewModel { Username = "alice", Password = "secret", ReturnUrl = "/dashboard" };

        var result = await controller.Login(model);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/dashboard", redirect.Url);
    }

    [Fact]
    public async Task Logout_SignsOutAndRedirectsToHome()
    {
        var httpContext = CreateHttpContextWithFakeAuth();
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider()),
            Url = new FakeUrlHelper()
        };

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.Equal("Signed out", controller.TempData["LoginMessage"]);
    }

    private static DefaultHttpContext CreateHttpContextWithFakeAuth()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.AspNetCore.Authentication.IAuthenticationService, FakeAuthenticationService>();
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }
}
