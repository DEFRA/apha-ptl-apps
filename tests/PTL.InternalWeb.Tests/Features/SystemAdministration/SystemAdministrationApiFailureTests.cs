using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Polly.CircuitBreaker;
using Polly.Timeout;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

public class SystemAdministrationApiFailureTests
{
    private static (SystemAdministrationController Controller, ActionExecutedContext Context) Arrange(
        Exception? exception,
        string method = "GET",
        string? referer = null,
        CancellationToken requestAborted = default)
    {
        var controller = SystemAdministrationControllerFactory.Create();
        controller.Url = new FakeUrlHelper();

        var httpContext = controller.ControllerContext.HttpContext;
        httpContext.Request.Method = method;
        httpContext.RequestAborted = requestAborted;
        if (referer is not null)
        {
            httpContext.Request.Headers.Referer = referer;
        }

        var context = new ActionExecutedContext(
            new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor { DisplayName = "Countries" }),
            [],
            controller)
        {
            Exception = exception
        };

        return (controller, context);
    }

    public static TheoryData<Exception> UnavailableExceptions() =>
        new()
        {
            new HttpRequestException("down"),
            new TimeoutRejectedException("timed out"),
            new BrokenCircuitException("open"),
            new TaskCanceledException("http timeout")
        };

    [Theory]
    [MemberData(nameof(UnavailableExceptions))]
    public void OnActionExecuted_WhenTheApiIsUnavailable_ShowsAnErrorBannerAndGoesToTheLandingPage(Exception exception)
    {
        var (controller, context) = Arrange(exception);

        controller.OnActionExecuted(context);

        Assert.True(context.ExceptionHandled);
        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Contains("Please try again later", notification.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OnActionExecuted_WhenASaveFails_ReturnsToThePageItWasPostedFrom()
    {
        var (controller, context) = Arrange(new HttpRequestException("down"), "POST", "https://localhost/System%20Administration/Countries?x=1");

        controller.OnActionExecuted(context);

        var redirect = Assert.IsType<LocalRedirectResult>(context.Result);
        Assert.Equal("/System%20Administration/Countries?x=1", redirect.Url);
        Assert.True(context.ExceptionHandled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a uri")]
    public void OnActionExecuted_WhenASaveFailsWithoutAUsableReferer_GoesToTheLandingPage(string? referer)
    {
        var (controller, context) = Arrange(new HttpRequestException("down"), "POST", referer);

        controller.OnActionExecuted(context);

        Assert.IsType<RedirectToActionResult>(context.Result);
    }

    [Fact]
    public void OnActionExecuted_WhenTheRequestWasAborted_LeavesTheExceptionAlone()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var exception = new OperationCanceledException(aborted.Token);
        var (controller, context) = Arrange(exception, requestAborted: aborted.Token);

        controller.OnActionExecuted(context);

        Assert.False(context.ExceptionHandled);
        Assert.Same(exception, context.Exception);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public void OnActionExecuted_ForAnyOtherException_LeavesItAlone()
    {
        var (controller, context) = Arrange(new InvalidOperationException("bug"));

        controller.OnActionExecuted(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Fact]
    public void OnActionExecuted_WhenTheActionSucceeded_DoesNothing()
    {
        var (controller, context) = Arrange(null);

        controller.OnActionExecuted(context);

        Assert.Null(context.Result);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public void OnActionExecuted_WithoutAContext_Throws()
    {
        var controller = SystemAdministrationControllerFactory.Create();

        Assert.Throws<ArgumentNullException>(() => controller.OnActionExecuted(null!));
    }
}
