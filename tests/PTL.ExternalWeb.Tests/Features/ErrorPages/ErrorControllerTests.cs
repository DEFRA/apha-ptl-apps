using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ExternalWeb.Features.ErrorPages;

namespace PTL.ExternalWeb.Tests.Features.ErrorPages;

public class ErrorControllerTests
{
    private static ErrorController Controller(string traceIdentifier = "trace-1") => new()
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { TraceIdentifier = traceIdentifier } },
    };

    [Fact]
    public void Index_NotFound_ShowsPageNotFound_AndKeeps404()
    {
        var controller = Controller();

        var result = controller.Index(StatusCodes.Status404NotFound);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("PageNotFound", view.ViewName);
        Assert.Null(view.Model);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Fact]
    public void Index_Forbidden_ShowsTheAccessDeniedPage_AndKeeps403()
    {
        var controller = Controller();

        var result = controller.Index(StatusCodes.Status403Forbidden);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ErrorController.AccessDeniedView, view.ViewName);
        Assert.Equal("/Features/Account/Views/AccessDenied.cshtml", view.ViewName);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    [InlineData(StatusCodes.Status503ServiceUnavailable)]
    public void Index_OtherStatus_WithoutAnActivity_ShowsErrorWithTheTraceIdentifier(int statusCode)
    {
        Assert.Null(Activity.Current);
        var controller = Controller("trace-42");

        var result = controller.Index(statusCode);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Error", view.ViewName);
        Assert.Equal("trace-42", Assert.IsType<ErrorViewModel>(view.Model).RequestId);
        Assert.Equal(statusCode, controller.Response.StatusCode);
    }

    [Fact]
    public void Index_OtherStatus_WithinAnActivity_ShowsErrorWithTheActivityId()
    {
        using var activity = new Activity("request").Start();
        var controller = Controller();

        var result = controller.Index(StatusCodes.Status503ServiceUnavailable);

        var model = Assert.IsType<ErrorViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(activity.Id, model.RequestId);
        Assert.True(model.ShowRequestId);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, controller.Response.StatusCode);
    }
}
