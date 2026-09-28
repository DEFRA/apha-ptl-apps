using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Auth.Cidm;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Account;

public class AccountControllerTests
{
    [Fact]
    public void Login_Get_WithLocalReturnUrl_ChallengesWithReturnUrl()
    {
        var controller = new AccountController { Url = new FakeUrlHelper() };

        var result = controller.Login("/dashboard");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal([CidmAuthenticationDefaults.AuthenticationScheme], challenge.AuthenticationSchemes);
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
    public void SignedOut_ReturnsView()
    {
        var controller = new AccountController();

        var result = controller.SignedOut();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Login_Post_ReturnsNotFound()
    {
        var controller = new AccountController();

        var result = await controller.Login(new AccountViewModel());

        Assert.IsType<NotFoundResult>(result);
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
    public async Task Logout_SignsOutCookieDirectlyAndReturnsCidmSignOutResult()
    {
        var httpContext = CreateHttpContextWithFakeAuth(out var authService);
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new FakeUrlHelper()
        };

        var result = await controller.Logout();

        // Signed out separately, not via a single SignOut(cookie, oidc) result sharing one
        // AuthenticationProperties - see the comment on Logout() for why (CookieAuthenticationHandler
        // also honours RedirectUri and would otherwise hijack the response with its own redirect).
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, authService.SignOutSchemes);
        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal([CidmAuthenticationDefaults.AuthenticationScheme], signOut.AuthenticationSchemes);
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
