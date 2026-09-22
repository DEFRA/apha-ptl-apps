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
        Assert.Contains("Email", controller.ModelState.Keys);
        Assert.Contains("Enter an email.", controller.ModelState["Email"].Errors.Select(e => e.ErrorMessage));
    }

    private static DefaultHttpContext CreateHttpContextWithFakeAuth()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService, FakeAuthenticationService>();
        services.AddSingleton<IUrlHelperFactory, TestUrlHelperFactory>();
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private sealed class TestAccountController : PtlAccountControllerBase<TestCredentials>
    {
        protected override string PostLoginRedirectController => "Home";

        protected override TestCredentials CreateLoginModel(string? returnUrl = null) => new() { ReturnUrl = returnUrl };
    }

    private sealed class TestCredentials : IAccountCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

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
