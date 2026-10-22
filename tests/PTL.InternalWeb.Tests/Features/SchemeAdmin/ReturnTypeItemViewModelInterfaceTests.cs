using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.InternalWeb.Features.SchemeAdmin;

namespace PTL.InternalWeb.Tests.Features.SchemeAdmin;

// Covers IReturnTypeItemListViewModel's explicit interface implementations on
// TestResultItemTypeViewModel/TestMethodItemTypeViewModel - these literal field-name/action-name
// members are read by _ReturnTypeItemAdminList.cshtml but aren't otherwise exercised directly by
// controller-level tests, leaving them reported as uncovered.
public class ReturnTypeItemViewModelInterfaceTests
{
    [Fact]
    public void TestResultItemTypeViewModel_ExplicitInterfaceMembers_ReturnExpectedValues()
    {
        var addReturnTypeId = Guid.NewGuid();
        var model = new TestResultItemTypeViewModel { Add = new TestResultItemTypeAddViewModel { TestReturnValueTypeId = addReturnTypeId } };
        IReturnTypeItemListViewModel view = model;

        Assert.Equal("Test Result Items", view.PageHeading);
        Assert.Equal(100, view.EditNameMaxLength);
        Assert.Equal("TestResultItemType", view.ActionName);
        Assert.Equal("TestResultItemTypeSave", view.SaveActionName);
        Assert.Equal("TestResultItemTypeRemove", view.RemoveActionName);
        Assert.Equal("testResultItemTypeEditForm", view.EditFormId);
        Assert.Equal("Edit.TestResultItemTypeId", view.EditIdFieldName);
        Assert.Equal("testResultItemTypeId", view.RemoveIdRouteParamName);
        Assert.Equal(addReturnTypeId.ToString(), view.AddTestReturnValueTypeId);
    }

    [Fact]
    public void TestMethodItemTypeViewModel_ExplicitInterfaceMembers_ReturnExpectedValues()
    {
        var addReturnTypeId = Guid.NewGuid();
        var model = new TestMethodItemTypeViewModel { Add = new TestMethodItemTypeAddViewModel { TestReturnValueTypeId = addReturnTypeId } };
        IReturnTypeItemListViewModel view = model;

        Assert.Equal("Test Method Items", view.PageHeading);
        Assert.Equal(100, view.EditNameMaxLength);
        Assert.Equal("TestMethodItemType", view.ActionName);
        Assert.Equal("TestMethodItemTypeSave", view.SaveActionName);
        Assert.Equal("TestMethodItemTypeRemove", view.RemoveActionName);
        Assert.Equal("testMethodItemTypeEditForm", view.EditFormId);
        Assert.Equal("Edit.TestMethodItemTypeId", view.EditIdFieldName);
        Assert.Equal("testMethodItemTypeId", view.RemoveIdRouteParamName);
        Assert.Equal(addReturnTypeId.ToString(), view.AddTestReturnValueTypeId);
    }
}
