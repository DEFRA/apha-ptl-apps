using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PTL.ApiClient.Tests;

public class PtlAccountControllerBaseTests
{
    [Fact]
    public void Login_Get_ReturnsViewWithModel()
    {
        var controller = new TestAccountController();

        var result = controller.Login("/dashboard");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TestCredentials>(view.Model);
        Assert.Equal("/dashboard", model.ReturnUrl);
    }

    [Fact]
    public async Task Login_Post_WithMissingCredentials_ReturnsViewWithValidationError()
    {
        var controller = new TestAccountController();
        var model = new TestCredentials { Username = "", Password = "" };

        var result = await controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Login_Post_WithNullModel_ReturnsViewWithNewModel()
    {
        var controller = new TestAccountController();

        var result = await controller.Login((TestCredentials)null!);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<TestCredentials>(view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Login_Post_WithValidCredentials_RedirectsToHome()
    {
        var controller = new TestAccountController
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContextWithFakeAuth() },
            Url = new TestUrlHelper()
        };

        var result = await controller.Login(new TestCredentials { Username = "alice", Password = "secret" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task Login_Post_WithValidLocalReturnUrl_RedirectsToReturnUrl()
    {
        var controller = new TestAccountController
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContextWithFakeAuth() },
            Url = new TestUrlHelper()
        };

        var result = await controller.Login(new TestCredentials { Username = "alice", Password = "secret", ReturnUrl = "/dashboard" });

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/dashboard", redirect.Url);
    }

    [Fact]
    public async Task Logout_RedirectsToHomeAndSetsTempData()
    {
        var httpContext = CreateHttpContextWithFakeAuth();
        var controller = new TestAccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider()),
            Url = new TestUrlHelper(httpContext)
        };

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.Equal("Signed out", controller.TempData["LoginMessage"] as string);
    }

    [Fact]
    public void AddFieldErrors_AddsMessagesToModelState()
    {
        var controller = new TestAccountController();
        var values = new Dictionary<string, string[]> { ["Email"] = ["Enter an email."] };

        ModelStateExtensions.AddFieldErrors(controller, values);

        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.TryGetValue("Email", out var emailEntry));
        Assert.Contains("Enter an email.", emailEntry.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public void ChallengeExternalLogin_WithLocalReturnUrl_UsesReturnUrl()
    {
        var httpContext = CreateHttpContextWithFakeAuth();
        var controller = new TestAccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new TestUrlHelper(httpContext)
        };

        var result = controller.CallChallengeExternalLogin("Cidm", "/dashboard");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal("/dashboard", challenge.Properties!.RedirectUri);
        Assert.Equal("Cidm", Assert.Single(challenge.AuthenticationSchemes));
    }

    [Fact]
    public void ChallengeExternalLogin_WithNonLocalReturnUrl_UsesPostLoginRedirect()
    {
        var httpContext = CreateHttpContextWithFakeAuth();
        var controller = new TestAccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new TestUrlHelper(httpContext)
        };

        var result = controller.CallChallengeExternalLogin("Cidm", "https://evil.example.com");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal("/Home/Index", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public void ChallengeExternalLogin_WithNoReturnUrl_UsesPostLoginRedirect()
    {
        var httpContext = CreateHttpContextWithFakeAuth();
        var controller = new TestAccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new TestUrlHelper(httpContext)
        };

        var result = controller.CallChallengeExternalLogin("Cidm", null);

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal("/Home/Index", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public async Task SignOutViaExternalSchemeAsync_WithIdToken_CarriesTokenToSignOut()
    {
        var authService = new FakeAuthenticationService(idToken: "captured-id-token");
        var httpContext = CreateHttpContextWithFakeAuth(authService);
        var controller = new TestAccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new TestUrlHelper(httpContext)
        };

        var result = await controller.CallSignOutViaExternalSchemeAsync("Cidm");

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal("captured-id-token", signOut.Properties!.GetTokenValue("id_token"));
        Assert.Equal("Cidm", Assert.Single(signOut.AuthenticationSchemes));
    }

    [Fact]
    public async Task SignOutViaExternalSchemeAsync_WithoutIdToken_SignsOutWithNoStoredToken()
    {
        var authService = new FakeAuthenticationService(idToken: null);
        var httpContext = CreateHttpContextWithFakeAuth(authService);
        var controller = new TestAccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new TestUrlHelper(httpContext)
        };

        var result = await controller.CallSignOutViaExternalSchemeAsync("Cidm");

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Null(signOut.Properties!.GetTokenValue("id_token"));
    }

    private static DefaultHttpContext CreateHttpContextWithFakeAuth(IAuthenticationService? authenticationService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(authenticationService ?? new FakeAuthenticationService());
        services.AddSingleton<IUrlHelperFactory, TestUrlHelperFactory>();
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private sealed class TestAccountController : PtlAccountControllerBase<TestCredentials>
    {
        protected override string PostLoginRedirectController => "Home";

        protected override TestCredentials CreateLoginModel(string? returnUrl = null) => new() { ReturnUrl = returnUrl };

        public IActionResult CallChallengeExternalLogin(string authenticationScheme, string? returnUrl) =>
            ChallengeExternalLogin(authenticationScheme, returnUrl);

        public Task<IActionResult> CallSignOutViaExternalSchemeAsync(string authenticationScheme) =>
            SignOutViaExternalSchemeAsync(authenticationScheme);
    }

    private sealed class TestCredentials : IAccountCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
    }

    private sealed class FakeAuthenticationService(string? idToken = null) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (idToken is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
            var identity = new ClaimsIdentity();
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, "Cookies");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }

    private sealed class TestUrlHelper : UrlHelper
    {
        public TestUrlHelper(HttpContext? httpContext = null)
            : base(new ActionContext
            {
                HttpContext = httpContext ?? new DefaultHttpContext(),
                RouteData = new RouteData(),
                ActionDescriptor = new ActionDescriptor()
            })
        {
        }

        public override bool IsLocalUrl(string? url) => url is not null && url.StartsWith('/');

        public override string? Action(UrlActionContext actionContext) => $"/{actionContext.Controller}/{actionContext.Action}";
    }

    private sealed class TestUrlHelperFactory : IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext context) => new TestUrlHelper(context.HttpContext);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
