using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Auth.Entra;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Account;

public class AccountControllerTests
{
    [Fact]
    public void Login_Get_WithLocalReturnUrl_ChallengesWithReturnUrl()
    {
        var controller = new AccountController { Url = new FakeUrlHelper() };

        var result = controller.Login("/dashboard");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal([EntraAuthenticationDefaults.AuthenticationScheme], challenge.AuthenticationSchemes);
        Assert.Equal("/dashboard", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public void Login_Get_WithoutReturnUrl_ChallengesWithHomeIndex()
    {
        var controller = new AccountController { Url = new FakeUrlHelper() };

        var result = controller.Login();

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal("/Home/Index", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public void Login_Get_WithNonLocalReturnUrl_ChallengesWithHomeIndex()
    {
        var controller = new AccountController { Url = new FakeUrlHelper() };

        var result = controller.Login("https://evil.example.com/phish");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal("/Home/Index", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public async Task Login_Post_ReturnsNotFound()
    {
        var controller = new AccountController();

        var result = await controller.Login(new AccountViewModel());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Login_Post_WithInvalidModelState_ReturnsBadRequest()
    {
        var controller = new AccountController();
        controller.ModelState.AddModelError("Username", "Required");

        var result = await controller.Login(new AccountViewModel());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void SignedOut_ReturnsView()
    {
        var controller = new AccountController();

        var result = controller.SignedOut();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void NotPermitted_ReturnsView()
    {
        var controller = new AccountController();

        var result = controller.NotPermitted();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void CreateLoginModel_ReturnsViewModelWithGivenReturnUrl()
    {
        // CreateLoginModel is required to satisfy PtlAccountControllerBase's abstract contract, but
        // this app's own Login(GET)/Login(POST) overrides never call it - invoked via reflection so
        // the (otherwise dead-for-this-app) implementation still has direct test coverage.
        var controller = new AccountController();
        var method = typeof(AccountController).GetMethod("CreateLoginModel", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var model = (AccountViewModel)method.Invoke(controller, ["/dashboard"])!;

        Assert.Equal("/dashboard", model.ReturnUrl);
    }

    [Fact]
    public async Task Logout_SignsOutCookieDirectlyAndReturnsEntraSignOutResult()
    {
        var httpContext = CreateHttpContextWithFakeAuth(out var authService);
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new FakeUrlHelper()
        };

        var result = await controller.Logout();

        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, authService.SignOutSchemes);
        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal([EntraAuthenticationDefaults.AuthenticationScheme], signOut.AuthenticationSchemes);
        Assert.Equal("/Account/SignedOut", signOut.Properties!.RedirectUri);
    }

    private static DefaultHttpContext CreateHttpContextWithFakeAuth(out FakeAuthenticationService authService)
    {
        authService = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authService);
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }
}
