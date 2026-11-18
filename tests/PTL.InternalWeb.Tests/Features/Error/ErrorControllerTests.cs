using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.InternalWeb.Features.Error;

namespace PTL.InternalWeb.Tests.Features.Error;

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

        Assert.Equal("PageNotFound", Assert.IsType<ViewResult>(result).ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status500InternalServerError)]
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

        Assert.Equal(activity.Id, Assert.IsType<ErrorViewModel>(Assert.IsType<ViewResult>(result).Model).RequestId);
    }
}
