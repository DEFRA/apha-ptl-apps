using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using PTL.InternalWeb.AuditReason;

namespace PTL.InternalWeb.Tests.AuditReason;

public class AuditReasonControllerExtensionsTests
{
    private sealed class TestController : Controller;

    private static AuditReasonPageViewModel Model() => new()
    {
        AuditObjectName = "Scheme PT1234: Test Scheme",
        AuditWhy = "kept reason",
        CancelUrl = "/Scheme/Index",
    };

    private static TestController ControllerPosting(Dictionary<string, string?[]> form)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.PathBase = "/app";
        context.Request.Path = "/Scheme/Edit/1";
        context.Request.QueryString = new QueryString("?x=1");
        context.Request.Form = new FormCollection(form.ToDictionary(kv => kv.Key, kv => new StringValues(kv.Value)));
        return new TestController { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    [Fact]
    public void AuditReasonPage_ReturnsTheSharedViewWithTheCallersContentUnchanged()
    {
        var controller = ControllerPosting(new() { ["Name"] = ["Scheme"] });

        var view = controller.AuditReasonPage(Model());

        Assert.Equal("AuditReasonPage", view.ViewName);
        var model = Assert.IsType<AuditReasonPageViewModel>(view.Model);
        Assert.Equal("Scheme PT1234: Test Scheme", model.AuditObjectName);
        Assert.Equal("kept reason", model.AuditWhy);
        Assert.Equal("/Scheme/Index", model.CancelUrl);
    }

    [Fact]
    public void AuditReasonPage_PostsBackToTheUrlBeingHandled()
    {
        var controller = ControllerPosting(new() { ["Name"] = ["Scheme"] });

        var model = Assert.IsType<AuditReasonPageViewModel>(controller.AuditReasonPage(Model()).Model);

        Assert.Equal("/app/Scheme/Edit/1?x=1", model.PostUrl);
    }

    [Fact]
    public void AuditReasonPage_EchoesEveryFieldExceptTheTokenAndTheOnesThePageOwns()
    {
        var controller = ControllerPosting(new()
        {
            ["__RequestVerificationToken"] = ["t"],
            ["IsAuditStep"] = ["false"],
            ["AuditObjectName"] = ["Scheme PT1234: Test Scheme"],
            ["AuditWhy"] = ["typed"],
            ["Name"] = ["Scheme"],
            ["Ids"] = ["a", "b"],
            ["confirmation"] = ["confirm"],
        });

        var model = Assert.IsType<AuditReasonPageViewModel>(controller.AuditReasonPage(Model()).Model);

        Assert.Equal(
            [new("Name", "Scheme"), new("Ids", "a"), new("Ids", "b"), new("confirmation", "confirm")],
            model.Fields);
    }

    [Fact]
    public void AuditReasonPage_AFieldWithANullValue_IsEchoedAsAnEmptyString()
    {
        var controller = ControllerPosting(new() { ["Name"] = [null] });

        var model = Assert.IsType<AuditReasonPageViewModel>(controller.AuditReasonPage(Model()).Model);

        Assert.Equal([new KeyValuePair<string, string>("Name", string.Empty)], model.Fields);
    }

    [Fact]
    public void AuditReasonPage_ARequestWithoutAForm_HasNoFieldsAndKeepsTheUrl()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/Scheme/Edit/1";
        var controller = new TestController { ControllerContext = new ControllerContext { HttpContext = context } };

        var model = Assert.IsType<AuditReasonPageViewModel>(controller.AuditReasonPage(Model()).Model);

        Assert.Empty(model.Fields);
        Assert.Equal("/Scheme/Edit/1", model.PostUrl);
    }

    [Fact]
    public void AuditReasonPage_WithoutAnHttpContext_HasNoFieldsAndNoPostUrl()
    {
        var controller = new TestController();

        var view = controller.AuditReasonPage(Model());

        var model = Assert.IsType<AuditReasonPageViewModel>(view.Model);
        Assert.Empty(model.Fields);
        Assert.Equal(string.Empty, model.PostUrl);
        Assert.Equal("kept reason", model.AuditWhy);
    }

    [Fact]
    public void AuditReasonFlow_NamesTheFieldsTheSchemeFormBinds()
    {
        Assert.Equal(nameof(PTL.InternalWeb.Features.Scheme.SchemeFormViewModel.IsAuditStep), AuditReasonFlow.IsAuditStepField);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Scheme.SchemeFormViewModel.AuditObjectName), AuditReasonFlow.AuditObjectNameField);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Scheme.SchemeFormViewModel.AuditWhy), AuditReasonFlow.AuditWhyField);
    }
}
