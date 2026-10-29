using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Viewer;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Navigation;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

// Manage Viewers (legacy ManageViewers.aspx): row edit/update/cancel/add only change the pending list; page Save/Apply
// writes it; Cancel discards it; Remove marks the row removed and is blocked while the viewer is in use.
public class ViewerManagementControllerTests
{
    private static readonly Guid IdA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid IdB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static ViewerResponse Persisted(Guid id, string name, string email) => new(id, name, email, [], []);

    private static ViewerRowViewModel Row(Guid? id, string name, string email) => new() { ViewerId = id, Name = name, Email = email };

    private static ViewerManagementViewModel Page(params ViewerRowViewModel[] rows) => new() { Rows = [.. rows] };

    private static (SystemAdministrationController Controller, FakeViewerApiClient Api) Arrange(params ViewerResponse[] viewers)
    {
        var api = new FakeViewerApiClient { Viewers = viewers };
        return (SystemAdministrationControllerFactory.Create(viewerApiClient: api), api);
    }

    private static ViewerManagementViewModel PageModel(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.ViewerManagement), view.ViewName);
        return Assert.IsType<ViewerManagementViewModel>(view.Model);
    }

    private static void AssertNothingWritten(FakeViewerApiClient api)
    {
        Assert.Empty(api.CreateRequests);
        Assert.Empty(api.UpdateCalls);
        Assert.Empty(api.DeleteCalls);
    }

    [Fact]
    public async Task Get_BuildsRowsSortedByNameIgnoringCase()
    {
        var (controller, _) = Arrange(Persisted(IdA, "zoe", "zoe@example.com"), Persisted(IdB, "Alan", "alan@example.com"));

        var model = PageModel(await controller.ViewerManagement(CancellationToken.None));

        Assert.Equal(["Alan", "zoe"], model.Rows.Select(r => r.Name));
        Assert.Equal(IdB, model.Rows[0].ViewerId);
        Assert.Equal("alan@example.com", model.Rows[0].Email);
        Assert.All(model.Rows, r => Assert.False(r.IsEditing));
        Assert.Null(model.EditIndex);
    }

    [Fact]
    public async Task Get_WithNoViewers_BuildsAnEmptyPage()
    {
        var (controller, _) = Arrange();

        var model = PageModel(await controller.ViewerManagement(CancellationToken.None));

        Assert.Empty(model.Rows);
    }

    [Fact]
    public async Task Edit_PutsTheRowInEditModeWithItsCurrentValuesAndWritesNothing()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"));

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Edit, 1, CancellationToken.None));

        Assert.Equal(1, result.EditIndex);
        Assert.Equal("Zoe", result.Edit.Name);
        Assert.Equal("zoe@example.com", result.Edit.Email);
        Assert.False(result.Rows[0].IsEditing);
        Assert.True(result.Rows[1].IsEditing);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(null)]
    public async Task Edit_WithARowIndexOutsideThePendingList_DoesNotEnterEditMode(int? rowIndex)
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"));

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Edit, rowIndex, CancellationToken.None));

        Assert.Null(result.EditIndex);
        Assert.All(result.Rows, r => Assert.False(r.IsEditing));
    }

    [Fact]
    public async Task StaleEditIndexBeyondTheRows_IsClearedWhenThePageIsRedisplayed()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"));
        model.EditIndex = 7;

        var result = PageModel(await controller.ViewerManagement(model, command: null, rowIndex: null, CancellationToken.None));

        Assert.Null(result.EditIndex);
    }

    [Fact]
    public async Task Update_Valid_ChangesThePendingRowOnlyAndLeavesEditMode()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"));
        var model = Page(Row(IdA, "Alan", "alan@example.com"));
        model.EditIndex = 0;
        model.Edit = new ViewerEditViewModel { Name = "  Alan Turing ", Email = " alan.turing@example.com " };

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Update, 0, CancellationToken.None));

        var row = Assert.Single(result.Rows);
        Assert.Equal("Alan Turing", row.Name);
        Assert.Equal("alan.turing@example.com", row.Email);
        Assert.Equal(IdA, row.ViewerId);
        Assert.Null(result.EditIndex);
        Assert.False(row.IsEditing);
        Assert.Equal(string.Empty, result.Edit.Name);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Update_Invalid_KeepsTheRowInEditModeWithFieldErrorsAndLeavesTheRowUnchanged()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"));
        model.EditIndex = 0;
        model.Edit = new ViewerEditViewModel { Name = string.Empty, Email = "not-an-email" };

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Update, 0, CancellationToken.None));

        Assert.Equal(0, result.EditIndex);
        Assert.True(result.Rows[0].IsEditing);
        Assert.Equal("Alan", result.Rows[0].Name);
        Assert.Equal("alan@example.com", result.Rows[0].Email);
        Assert.Equal("Enter a name", controller.ModelState["Edit.Name"]!.Errors[0].ErrorMessage);
        Assert.Equal("Enter an email address in the correct format", controller.ModelState["Edit.Email"]!.Errors[0].ErrorMessage);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task CancelEdit_LeavesEditModeAndKeepsEveryOtherPendingChange()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan (pending change)", "alan@example.com"), Row(null, "New Viewer", "new@example.com"));
        model.EditIndex = 1;
        model.Edit = new ViewerEditViewModel { Name = "typed but abandoned", Email = "x@example.com" };

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.CancelEdit, 1, CancellationToken.None));

        Assert.Null(result.EditIndex);
        Assert.Equal(["Alan (pending change)", "New Viewer"], result.Rows.Select(r => r.Name));
        Assert.All(result.Rows, r => Assert.False(r.IsEditing));
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Add_Valid_AppendsAPendingRowAndResetsTheAddFormWithoutCallingTheApi()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"));
        model.Add = new ViewerAddViewModel { Name = " Grace Hopper ", Email = " grace@example.com " };

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Add, null, CancellationToken.None));

        Assert.Equal(2, result.Rows.Count);
        var added = result.Rows[1];
        Assert.Null(added.ViewerId);
        Assert.Equal("Grace Hopper", added.Name);
        Assert.Equal("grace@example.com", added.Email);
        Assert.Equal(string.Empty, result.Add.Name);
        Assert.Equal(string.Empty, result.Add.Email);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Theory]
    [InlineData("", "grace@example.com", "Add.Name", "Enter a name")]
    [InlineData("Grace", "", "Add.Email", "Enter an email address")]
    [InlineData("Grace", "nope", "Add.Email", "Enter an email address in the correct format")]
    public async Task Add_Invalid_DoesNotAppendARowAndReportsTheFieldError(string name, string email, string key, string message)
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"));
        model.Add = new ViewerAddViewModel { Name = name, Email = email };

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Add, null, CancellationToken.None));

        Assert.Single(result.Rows);
        Assert.Equal(message, controller.ModelState[key]!.Errors[0].ErrorMessage);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Submit_WithNoRowInEditMode_BehavesAsAdd()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"));
        model.Add = new ViewerAddViewModel { Name = "Grace", Email = "grace@example.com" };

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Submit, null, CancellationToken.None));

        Assert.Equal(["Alan", "Grace"], result.Rows.Select(r => r.Name));
    }

    [Fact]
    public async Task Submit_WithARowInEditMode_BehavesAsUpdateOfThatRow()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"));
        model.EditIndex = 1;
        model.Edit = new ViewerEditViewModel { Name = "Zoe Q", Email = "zoeq@example.com" };
        model.Add = new ViewerAddViewModel { Name = "Ignored", Email = "ignored@example.com" };

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Submit, null, CancellationToken.None));

        Assert.Equal(["Alan", "Zoe Q"], result.Rows.Select(r => r.Name));
        Assert.Null(result.EditIndex);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Save_WritesOnlyNewAndChangedRowsThenNotifiesAndLeavesThePage()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"), Persisted(IdB, "Zoe", "zoe@example.com"));
        var model = Page(
            Row(IdA, "Alan", "alan@example.com"),
            Row(IdB, "Zoe", "zoe.new@example.com"),
            Row(null, "Grace", "grace@example.com"));

        var result = await controller.ViewerManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        var update = Assert.Single(api.UpdateCalls);
        Assert.Equal(IdB, update.ViewerId);
        Assert.Equal(new ViewerSaveRequest("Zoe", "zoe.new@example.com"), update.Request);
        Assert.Equal(new ViewerSaveRequest("Grace", "grace@example.com"), Assert.Single(api.CreateRequests));
        Assert.Empty(api.DeleteCalls);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Viewers saved successfully.", notification.Message);
    }

    [Fact]
    public async Task Save_WithNothingChanged_WritesNothingButStillLeavesThePageWithTheNotification()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"));
        var model = Page(Row(IdA, "Alan", "alan@example.com"));

        var result = await controller.ViewerManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        AssertNothingWritten(api);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task Save_ForAViewerNoLongerInTheDatabase_UpdatesItByIdRatherThanSkippingIt()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"));

        await controller.ViewerManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.Equal(IdA, Assert.Single(api.UpdateCalls).ViewerId);
    }

    [Fact]
    public async Task Save_WhenTheApiRejectsARow_KeepsEveryRowPendingWithAPrefixedRowsErrorAndNoNotification()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"));
        var created = new ViewerResponse(Guid.NewGuid(), "Grace", "grace@example.com", [], []);
        api.SaveResultFor = request => request.Name == "Bad"
            ? new ViewerSaveResult(false, null, new Dictionary<string, string[]> { ["Email"] = ["Enter an email address", "Email already used"] })
            : new ViewerSaveResult(true, created, new Dictionary<string, string[]>());
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(null, "Bad", "bad@example.com"), Row(null, "Grace", "grace@example.com"));
        model.EditIndex = 1;

        var result = await controller.ViewerManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        var page = PageModel(result);
        Assert.Equal(["Alan", "Bad", "Grace"], page.Rows.Select(r => r.Name));
        Assert.Equal(["Bad: Enter an email address", "Bad: Email already used"], controller.ModelState["Rows"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Null(page.Rows[1].ViewerId);
        Assert.Equal(created.ViewerId, page.Rows[2].ViewerId);
        Assert.Null(page.EditIndex);
        Assert.All(page.Rows, r => Assert.False(r.IsEditing));
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Apply_WritesThenStaysOnThePageWithTheNotificationAndEditModeCleared()
    {
        var (controller, api) = Arrange();
        var created = new ViewerResponse(Guid.NewGuid(), "Grace", "grace@example.com", [], []);
        api.SaveResult = new ViewerSaveResult(true, created, new Dictionary<string, string[]>());
        var model = Page(Row(null, "Grace", "grace@example.com"));
        model.EditIndex = 0;

        var result = await controller.ViewerManagement(model, PendingListCommands.Apply, null, CancellationToken.None);

        var page = PageModel(result);
        Assert.Null(page.EditIndex);
        Assert.False(page.Rows[0].IsEditing);
        Assert.Equal(created.ViewerId, page.Rows[0].ViewerId);
        Assert.Single(api.CreateRequests);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Viewers saved successfully.", notification.Message);
    }

    [Fact]
    public async Task Apply_WhenTheApiRejectsARow_StaysOnThePageWithTheErrorAndNoNotification()
    {
        var (controller, api) = Arrange();
        api.SaveResult = new ViewerSaveResult(false, null, new Dictionary<string, string[]> { ["Email"] = ["Enter an email address"] });
        var model = Page(Row(null, "Grace", "grace@example.com"));

        var result = await controller.ViewerManagement(model, PendingListCommands.Apply, null, CancellationToken.None);

        PageModel(result);
        Assert.Equal("Grace: Enter an email address", controller.ModelState["Rows"]!.Errors[0].ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Cancel_LeavesForSystemAdministrationWithoutWritingOrNotifying()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(null, "Grace", "grace@example.com"), Row(IdA, "Changed", "changed@example.com"));

        var result = await controller.ViewerManagement(model, PendingListCommands.Cancel, null, CancellationToken.None);

        Assert.Equal(nameof(SystemAdministrationController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(null)]
    public async Task Remove_WithARowIndexOutsideThePendingList_RedisplaysThePageWithoutCallingTheApi(int? rowIndex)
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"));
        var model = Page(Row(IdA, "Alan", "alan@example.com"));

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Remove, rowIndex, CancellationToken.None));

        Assert.Single(result.Rows);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Remove_OfAnUnsavedRow_DropsItFromThePendingListWithoutCallingTheApiAndKeepsTheEditRowSelected()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(null, "Grace", "grace@example.com"), Row(IdB, "Zoe", "zoe@example.com"));
        model.EditIndex = 1;
        model.Edit = new ViewerEditViewModel { Name = "Zoe Q", Email = "zoeq@example.com" };

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Remove, 0, CancellationToken.None));

        var remaining = Assert.Single(result.Rows);
        Assert.Equal("Zoe", remaining.Name);
        Assert.Equal(0, result.EditIndex);
        Assert.True(remaining.IsEditing);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Remove_OfTheUnsavedRowBeingEdited_LeavesEditMode()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdB, "Zoe", "zoe@example.com"), Row(null, "Grace", "grace@example.com"));
        model.EditIndex = 1;

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Remove, 1, CancellationToken.None));

        Assert.Single(result.Rows);
        Assert.Null(result.EditIndex);
    }

    [Fact]
    public async Task Remove_WhenAssignedToSchemesAndParticipants_IsBlockedWithTheAssignmentsListed()
    {
        var (controller, api) = Arrange(new ViewerResponse(
            IdA, "Alan", "alan@example.com",
            [new ViewerSchemeResponse("SFW1234", "Heavy Metals")],
            [new ViewerParticipantResponse("LAB001", "Example Lab")]));
        var model = Page(Row(IdA, "Alan", "alan@example.com"));

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Remove, 0, CancellationToken.None));

        Assert.True(result.MessageIsError);
        Assert.Contains("cannot be removed", result.Message, StringComparison.Ordinal);
        Assert.Equal(["Scheme SFW1234: Heavy Metals", "Participant LAB001: Example Lab"], result.MessageDetails);
        Assert.Single(result.Rows);
        Assert.False(result.Rows[0].IsRemoved);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Remove_WhenAssignedToEitherSchemesOrParticipants_IsBlocked(bool hasScheme, bool hasParticipant)
    {
        var (controller, api) = Arrange(new ViewerResponse(
            IdA, "Alan", "alan@example.com",
            hasScheme ? [new ViewerSchemeResponse("SFW1234", "Heavy Metals")] : [],
            hasParticipant ? [new ViewerParticipantResponse("LAB001", "Example Lab")] : []));
        var model = Page(Row(IdA, "Alan", "alan@example.com"));

        var result = PageModel(await controller.ViewerManagement(model, PendingListCommands.Remove, 0, CancellationToken.None));

        Assert.True(result.MessageIsError);
        Assert.Single(result.MessageDetails);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Remove_MarksTheRowRemovedOnTheRedisplayedPageWithoutDeletingNotifyingOrRedirecting()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"), Persisted(IdB, "Zoe", "zoe@example.com"));
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"));

        var result = await controller.ViewerManagement(model, PendingListCommands.Remove, 0, CancellationToken.None);

        Assert.IsNotType<RedirectToActionResult>(result);
        var page = PageModel(result);
        Assert.Equal(2, page.Rows.Count);
        Assert.True(page.Rows[0].IsRemoved);
        Assert.Equal(IdA, page.Rows[0].ViewerId);
        Assert.False(page.Rows[1].IsRemoved);
        Assert.Null(page.Message);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Remove_ForAViewerTheApiNoLongerReturns_StillMarksItRemoved()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"));

        var page = PageModel(await controller.ViewerManagement(model, PendingListCommands.Remove, 0, CancellationToken.None));

        Assert.True(Assert.Single(page.Rows).IsRemoved);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Remove_ForTheRowInEditMode_LeavesEditMode()
    {
        var (controller, _) = Arrange(Persisted(IdA, "Alan", "alan@example.com"), Persisted(IdB, "Zoe", "zoe@example.com"));
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"));
        model.EditIndex = 1;

        var page = PageModel(await controller.ViewerManagement(model, PendingListCommands.Remove, 1, CancellationToken.None));

        Assert.True(page.Rows[1].IsRemoved);
        Assert.Null(page.EditIndex);
        Assert.All(page.Rows, r => Assert.False(r.IsEditing));
    }

    [Fact]
    public async Task Remove_ForADifferentRowThanTheOneInEditMode_KeepsEditMode()
    {
        var (controller, _) = Arrange(Persisted(IdA, "Alan", "alan@example.com"), Persisted(IdB, "Zoe", "zoe@example.com"));
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"));
        model.EditIndex = 1;

        var page = PageModel(await controller.ViewerManagement(model, PendingListCommands.Remove, 0, CancellationToken.None));

        Assert.True(page.Rows[0].IsRemoved);
        Assert.Equal(1, page.EditIndex);
        Assert.True(page.Rows[1].IsEditing);
    }

    [Fact]
    public async Task Save_DeletesEveryRemovedRowThenDropsItNotifiesAndLeavesThePage()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"), Persisted(IdB, "Zoe", "zoe@example.com"));
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"));
        model.Rows[0].IsRemoved = true;
        model.EditIndex = 1;

        var result = await controller.ViewerManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.Equal(nameof(SystemAdministrationController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal([IdA], api.DeleteCalls);
        Assert.Equal(["Zoe"], model.Rows.Select(r => r.Name));
        Assert.Null(model.EditIndex);
        Assert.Empty(api.CreateRequests);
        Assert.Empty(api.UpdateCalls);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Viewers saved successfully.", notification.Message);
    }

    [Fact]
    public async Task Save_WritesNewAndChangedRowsButNeverAChangedRowThatIsRemoved()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"), Persisted(IdB, "Zoe", "zoe@example.com"));
        var model = Page(Row(IdA, "Alan renamed", "alan@example.com"), Row(IdB, "Zoe", "zoe.new@example.com"), Row(null, "Grace", "grace@example.com"));
        model.Rows[0].IsRemoved = true;

        await controller.ViewerManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.Equal([IdA], api.DeleteCalls);
        Assert.Equal(IdB, Assert.Single(api.UpdateCalls).ViewerId);
        Assert.Equal("Grace", Assert.Single(api.CreateRequests).Name);
    }

    [Fact]
    public async Task Save_OfARemovedRowThatWasNeverSaved_JustDropsItWithoutCallingTheApi()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(null, "Grace", "grace@example.com"));
        model.Rows[0].IsRemoved = true;

        var result = await controller.ViewerManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(model.Rows);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Save_WhenOneDeleteFails_KeepsThatRowRemovedWithItsNamedErrorAndDropsTheOthersWithNoNotification()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"), Persisted(IdB, "Zoe", "zoe@example.com"));
        api.DeleteResultFor = id => id == IdB ? new ViewerDeleteResponse(false, "Zoe is still assigned.") : new ViewerDeleteResponse(true, null);
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"), Row(null, "Grace", "grace@example.com"));
        model.Rows[0].IsRemoved = true;
        model.Rows[1].IsRemoved = true;

        var result = await controller.ViewerManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        var page = PageModel(result);
        Assert.Equal([IdA, IdB], api.DeleteCalls);
        Assert.Equal(["Zoe", "Grace"], page.Rows.Select(r => r.Name));
        Assert.True(page.Rows[0].IsRemoved);
        Assert.False(page.Rows[1].IsRemoved);
        Assert.Equal(["Zoe: Zoe is still assigned."], controller.ModelState["Rows"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Null(controller.TempData.GetNotification());
        Assert.Equal("Grace", Assert.Single(api.CreateRequests).Name);
    }

    [Fact]
    public async Task Save_WhenADeleteFailsWithoutAMessage_UsesTheDefaultMessage()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"));
        api.DeleteResult = new ViewerDeleteResponse(false, null);
        var model = Page(Row(IdA, "Alan", "alan@example.com"));
        model.Rows[0].IsRemoved = true;

        var page = PageModel(await controller.ViewerManagement(model, PendingListCommands.Save, null, CancellationToken.None));

        Assert.True(Assert.Single(page.Rows).IsRemoved);
        Assert.Equal("Alan: The viewer could not be removed.", Assert.Single(controller.ModelState["Rows"]!.Errors).ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Save_WhenADeleteSucceedsButAWriteIsRejected_DropsTheDeletedRowAndStaysOnThePageWithoutNotification()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"));
        api.SaveResult = new ViewerSaveResult(false, null, new Dictionary<string, string[]> { ["Email"] = ["Email already used"] });
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(null, "Grace", "grace@example.com"));
        model.Rows[0].IsRemoved = true;

        var page = PageModel(await controller.ViewerManagement(model, PendingListCommands.Save, null, CancellationToken.None));

        Assert.Equal([IdA], api.DeleteCalls);
        Assert.Equal(["Grace"], page.Rows.Select(r => r.Name));
        Assert.Equal("Grace: Email already used", Assert.Single(controller.ModelState["Rows"]!.Errors).ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Apply_DeletesRemovedRowsDropsThemAndStaysOnThePageWithTheNotification()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"), Persisted(IdB, "Zoe", "zoe@example.com"));
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"));
        model.Rows[1].IsRemoved = true;

        var result = await controller.ViewerManagement(model, PendingListCommands.Apply, null, CancellationToken.None);

        var page = PageModel(result);
        Assert.Equal([IdB], api.DeleteCalls);
        Assert.Equal(["Alan"], page.Rows.Select(r => r.Name));
        Assert.Equal("Viewers saved successfully.", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task Apply_WhenADeleteFails_KeepsTheRowRemovedAndShowsTheErrorWithoutNotification()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"));
        api.DeleteResult = new ViewerDeleteResponse(false, "Alan is still assigned.");
        var model = Page(Row(IdA, "Alan", "alan@example.com"));
        model.Rows[0].IsRemoved = true;

        var page = PageModel(await controller.ViewerManagement(model, PendingListCommands.Apply, null, CancellationToken.None));

        Assert.True(Assert.Single(page.Rows).IsRemoved);
        Assert.Equal("Alan: Alan is still assigned.", Assert.Single(controller.ModelState["Rows"]!.Errors).ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Cancel_WithRemovedRowsPending_LeavesWithoutDeletingAnything()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"));
        var model = Page(Row(IdA, "Alan", "alan@example.com"));
        model.Rows[0].IsRemoved = true;

        var result = await controller.ViewerManagement(model, PendingListCommands.Cancel, null, CancellationToken.None);

        Assert.Equal(nameof(SystemAdministrationController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Theory]
    [InlineData(PendingListCommands.Edit)]
    [InlineData(PendingListCommands.Update)]
    [InlineData(PendingListCommands.CancelEdit)]
    [InlineData(PendingListCommands.Add)]
    [InlineData(PendingListCommands.Remove)]
    [InlineData(PendingListCommands.Submit)]
    public async Task RowCommands_WithARemovedRowPending_StayOnThePageAndNeverDelete(string command)
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", "alan@example.com"), Persisted(IdB, "Zoe", "zoe@example.com"));
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"));
        model.Rows[0].IsRemoved = true;

        var result = await controller.ViewerManagement(model, command, 1, CancellationToken.None);

        Assert.IsNotType<RedirectToActionResult>(result);
        Assert.True(model.Rows[0].IsRemoved);
        Assert.Empty(api.DeleteCalls);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task EditIndexPointingAtARemovedRow_IsClearedWhenThePageIsRedisplayed()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"), Row(IdB, "Zoe", "zoe@example.com"));
        model.Rows[0].IsRemoved = true;
        model.EditIndex = 0;

        var page = PageModel(await controller.ViewerManagement(model, command: null, rowIndex: null, CancellationToken.None));

        Assert.Null(page.EditIndex);
        Assert.All(page.Rows, r => Assert.False(r.IsEditing));
    }

    [Fact]
    public async Task Add_AfterARowWasRemoved_KeepsTheRemovedRowInTheListSoIndicesStayContiguous()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"));
        model.Rows[0].IsRemoved = true;
        model.Add = new ViewerAddViewModel { Name = "Grace", Email = "grace@example.com" };

        var page = PageModel(await controller.ViewerManagement(model, PendingListCommands.Add, null, CancellationToken.None));

        Assert.Equal(["Alan", "Grace"], page.Rows.Select(r => r.Name));
        Assert.True(page.Rows[0].IsRemoved);
        Assert.False(page.Rows[1].IsRemoved);
    }

    [Fact]
    public async Task UnknownCommand_RedisplaysThePendingPageUnchanged()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"));

        var result = PageModel(await controller.ViewerManagement(model, "something-else", 0, CancellationToken.None));

        Assert.Single(result.Rows);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Commands_AreMatchedIgnoringCase()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan", "alan@example.com"));

        var result = PageModel(await controller.ViewerManagement(model, "EDIT", 0, CancellationToken.None));

        Assert.Equal(0, result.EditIndex);
    }
}
