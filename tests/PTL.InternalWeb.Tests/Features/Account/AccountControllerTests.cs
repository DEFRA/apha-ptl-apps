using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Account;

public class AccountControllerTests
{
    [Fact]
    public void Login_WithNoReturnUrl_ChallengesOpenIdConnectAndRedirectsToRoot()
    {
        var controller = new AccountController { Url = new FakeUrlHelper() };

        var result = controller.Login();

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal(new[] { OpenIdConnectDefaults.AuthenticationScheme }, challenge.AuthenticationSchemes);
        Assert.Equal("/", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public void Login_WithLocalReturnUrl_ChallengesAndPreservesReturnUrl()
    {
        var controller = new AccountController { Url = new FakeUrlHelper() };

        var result = controller.Login("/customer/index");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal("/customer/index", challenge.Properties!.RedirectUri);
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("//evil.example.com")]
    public void Login_WithExternalReturnUrl_IgnoresItAndRedirectsToRoot(string returnUrl)
    {
        // Guards against open-redirect: an attacker-supplied absolute/protocol-relative URL must
        // never be forwarded as the post-sign-in redirect target.
        var controller = new AccountController { Url = new FakeUrlHelper() };

        var result = controller.Login(returnUrl);

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal("/", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public void Logout_SignsOutOfCookieAndOpenIdConnectSchemes()
    {
        var controller = new AccountController { Url = new FakeUrlHelper() };

        var result = controller.Logout();

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal(
            new[] { CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme },
            signOut.AuthenticationSchemes);
        Assert.Equal("/Home/Index", signOut.Properties!.RedirectUri);
    }

    [Fact]
    public void AccessDenied_ReturnsViewWithReturnUrl()
    {
        var controller = new AccountController();

        var result = controller.AccessDenied("/customer/index");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AccessDeniedViewModel>(viewResult.Model);
        Assert.Equal("/customer/index", model.ReturnUrl);
    }
}

