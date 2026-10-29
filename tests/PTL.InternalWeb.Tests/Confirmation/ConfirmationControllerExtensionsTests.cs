using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Navigation;

namespace PTL.InternalWeb.Tests.Confirmation;

public class ConfirmationControllerExtensionsTests
{
    private sealed class TestController : Controller;

    private static TestController ControllerPosting(Dictionary<string, string[]> form)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.PathBase = "/app";
        context.Request.Path = "/Scheme/Edit/1";
        context.Request.QueryString = new QueryString("?x=1");
        context.Request.Form = new FormCollection(form.ToDictionary(kv => kv.Key, kv => new Microsoft.Extensions.Primitives.StringValues(kv.Value)));
        return new TestController { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    [Fact]
    public void ConfirmationPage_EchoesTheFormExceptTheTokenAndConfirmationAndPostsBackToTheSameUrl()
    {
        var controller = ControllerPosting(new()
        {
            ["__RequestVerificationToken"] = ["t"],
            ["confirmation"] = ["cancel"],
            ["Name"] = ["Scheme"],
            ["Ids"] = ["a", "b"],
        });

        var view = controller.ConfirmationPage(new ConfirmationPageViewModel { Title = "Remove" }, "Edit");

        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        var model = Assert.IsType<ConfirmationPageViewModel>(view.Model);
        Assert.Equal("/app/Scheme/Edit/1?x=1", model.PostUrl);
        Assert.Equal(
            [new("Name", "Scheme"), new("Ids", "a"), new("Ids", "b")],
            model.Fields);
        Assert.Equal("Edit", controller.ViewData[BreadcrumbRouteValues.Action]);
    }

    [Fact]
    public void ConfirmationPage_DoesNotTreatTheAuditReasonFieldsAsItsOwn()
    {
        var controller = ControllerPosting(new()
        {
            ["IsAuditStep"] = ["true"],
            ["AuditWhy"] = ["reason"],
            ["Name"] = ["Scheme"],
        });

        var model = Assert.IsType<ConfirmationPageViewModel>(controller.ConfirmationPage(new ConfirmationPageViewModel { Title = "Remove" }).Model);

        Assert.Equal([new("IsAuditStep", "true"), new("AuditWhy", "reason"), new("Name", "Scheme")], model.Fields);
        Assert.False(controller.ViewData.ContainsKey(BreadcrumbRouteValues.Action));
    }

    [Fact]
    public void ConfirmationPage_ARequestWithoutAForm_HasNoFieldsAndKeepsTheUrl()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/Invoice/Index";
        var controller = new TestController { ControllerContext = new ControllerContext { HttpContext = context } };

        var model = Assert.IsType<ConfirmationPageViewModel>(controller.ConfirmationPage(new ConfirmationPageViewModel { Title = "Remove" }).Model);

        Assert.Empty(model.Fields);
        Assert.Equal("/Invoice/Index", model.PostUrl);
        Assert.False(controller.ViewData.ContainsKey(BreadcrumbRouteValues.Action));
    }

    [Fact]
    public void ConfirmationPage_AFieldWithANullValue_IsEchoedAsAnEmptyString()
    {
        var controller = ControllerPosting(new() { ["Name"] = [null!] });

        var model = Assert.IsType<ConfirmationPageViewModel>(controller.ConfirmationPage(new ConfirmationPageViewModel { Title = "Remove" }).Model);

        Assert.Equal([new KeyValuePair<string, string>("Name", string.Empty)], model.Fields);
    }

    [Fact]
    public void ConfirmationPage_WithoutARequest_HasNoFieldsAndNoPostUrl()
    {
        var controller = new TestController();

        var model = Assert.IsType<ConfirmationPageViewModel>(controller.ConfirmationPage(new ConfirmationPageViewModel { Title = "Remove" }).Model);

        Assert.Empty(model.Fields);
        Assert.Equal(string.Empty, model.PostUrl);
    }

    [Theory]
    [InlineData("confirm", true, false)]
    [InlineData("cancel", false, true)]
    [InlineData(null, false, false)]
    [InlineData("Confirm", false, false)]
    public void ConfirmationFlow_RecognisesOnlyTheExactValues(string? value, bool confirmed, bool cancelled)
    {
        Assert.Equal(confirmed, ConfirmationFlow.IsConfirmed(value));
        Assert.Equal(cancelled, ConfirmationFlow.IsCancelled(value));
    }
}
