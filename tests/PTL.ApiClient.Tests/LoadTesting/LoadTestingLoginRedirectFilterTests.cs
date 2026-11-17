using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace PTL.ApiClient.Tests.LoadTesting;

public class LoadTestingLoginRedirectFilterTests
{
    private static LoadTestingLoginRedirectFilter CreateFilter(bool enabled) =>
        new(Options.Create(new LoadTestingOptions { Enabled = enabled }));

    private static ResourceExecutingContext CreateContext(
        string? controller = "Account",
        string? action = "Login",
        string method = "GET",
        ClaimsPrincipal? user = null,
        string query = "?returnUrl=/dashboard")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.QueryString = new QueryString(query);
        if (user is not null)
        {
            httpContext.User = user;
        }

        var descriptor = new ActionDescriptor();
        if (controller is not null)
        {
            descriptor.RouteValues["controller"] = controller;
        }

        if (action is not null)
        {
            descriptor.RouteValues["action"] = action;
        }

        return new ResourceExecutingContext(
            new ActionContext(httpContext, new RouteData(), descriptor),
            new List<IFilterMetadata>(),
            new List<IValueProviderFactory>());
    }

    [Fact]
    public void Enabled_UnauthenticatedGetOfAccountLogin_RedirectsToTheLoadTestingSignInKeepingReturnUrl()
    {
        var context = CreateContext();

        CreateFilter(enabled: true).OnResourceExecuting(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("LoadTesting", redirect.ControllerName);
        Assert.Equal("/dashboard", redirect.RouteValues!["returnUrl"]);
    }

    [Fact]
    public void Enabled_PrincipalWithoutAnIdentity_IsTreatedAsUnauthenticated()
    {
        var context = CreateContext(user: new ClaimsPrincipal(), query: string.Empty);

        CreateFilter(enabled: true).OnResourceExecuting(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Null(redirect.RouteValues!["returnUrl"]);
    }

    [Fact]
    public void Disabled_LeavesAccountLoginToTheIdentityProvider()
    {
        var context = CreateContext();

        CreateFilter(enabled: false).OnResourceExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void Enabled_AuthenticatedUser_IsNotRedirected()
    {
        var context = CreateContext(user: new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "tester")], "Cookies")));

        CreateFilter(enabled: true).OnResourceExecuting(context);

        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData(null, "Login", "GET")]
    [InlineData("Home", "Login", "GET")]
    [InlineData("Account", null, "GET")]
    [InlineData("Account", "Logout", "GET")]
    [InlineData("Account", "Login", "POST")]
    public void Enabled_AnyOtherRequest_IsNotRedirected(string? controller, string? action, string method)
    {
        var context = CreateContext(controller, action, method);

        CreateFilter(enabled: true).OnResourceExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnResourceExecuted_LeavesTheResultAlone()
    {
        var executing = CreateContext();
        var result = new OkResult();
        var executed = new ResourceExecutedContext(executing, executing.Filters) { Result = result };

        CreateFilter(enabled: true).OnResourceExecuted(executed);

        Assert.Same(result, executed.Result);
    }
}
