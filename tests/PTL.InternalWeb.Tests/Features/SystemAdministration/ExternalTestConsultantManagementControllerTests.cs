using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestConsultant;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

// Manage External Test Consultants (legacy ManageExternalTCs.aspx): row edit/update/cancel/add/remove and the Status
// toggle only change the pending list; page Save/Apply writes it; Cancel discards it; Remove and Status are refused
// while the consultant is allocated to schemes.
public class ExternalTestConsultantManagementControllerTests
{
    private static readonly Guid IdA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid IdB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static ExternalTestConsultantResponse Persisted(Guid id, string name, bool isInactive = false, int allocations = 0, string? schemes = null) =>
        new(id, name, "Science", $"{name.ToLowerInvariant()}@example.com", isInactive, isInactive ? new DateTime(2026, 1, 2) : null, allocations, schemes);

    private static ExternalTestConsultantRowViewModel Row(Guid? id, string name, bool isInactive = false) =>
        new() { ExternalTestConsultantId = id, Name = name, Department = "Science", Email = $"{name.ToLowerInvariant()}@example.com", IsInactive = isInactive };

    private static ExternalTestConsultantManagementViewModel Page(params ExternalTestConsultantRowViewModel[] rows) => new() { Rows = [.. rows] };

    private static (SystemAdministrationController Controller, FakeExternalTestConsultantApiClient Api) Arrange(params ExternalTestConsultantResponse[] consultants)
    {
        var api = new FakeExternalTestConsultantApiClient { TestConsultants = consultants };
        return (SystemAdministrationControllerFactory.Create(externalTestConsultantApiClient: api), api);
    }

    private static ExternalTestConsultantManagementViewModel PageModel(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.ExternalTestConsultantManagement), view.ViewName);
        return Assert.IsType<ExternalTestConsultantManagementViewModel>(view.Model);
    }

    private static void AssertNothingWritten(FakeExternalTestConsultantApiClient api)
    {
        Assert.Null(api.LastCreateRequest);
        Assert.Empty(api.UpdateCalls);
        Assert.Empty(api.SetStatusCalls);
        Assert.Empty(api.DeleteCalls);
    }

    private static Task<IActionResult> Post(SystemAdministrationController controller, ExternalTestConsultantManagementViewModel model, string? command, int? rowIndex = null) =>
        controller.ExternalTestConsultantManagement(model, command, rowIndex, CancellationToken.None);

    [Fact]
    public async Task Get_BuildsRowsSortedByNameIgnoringCase()
    {
        var (controller, _) = Arrange(Persisted(IdA, "zoe"), Persisted(IdB, "Alan", isInactive: true));

        var model = PageModel(await controller.ExternalTestConsultantManagement(CancellationToken.None));

        Assert.Equal(["Alan", "zoe"], model.Rows.Select(r => r.Name));
        Assert.Equal(IdB, model.Rows[0].ExternalTestConsultantId);
        Assert.True(model.Rows[0].IsInactive);
        Assert.NotNull(model.Rows[0].InactiveDate);
        Assert.All(model.Rows, r => Assert.False(r.IsEditing));
        Assert.Null(model.EditIndex);
    }

    [Fact]
    public async Task Edit_PutsTheRowInEditModeWithItsCurrentValuesAndWritesNothing()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"), Row(IdB, "Zoe"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Edit, 1));

        Assert.Equal(1, result.EditIndex);
        Assert.True(result.Rows[1].IsEditing);
        Assert.False(result.Rows[0].IsEditing);
        Assert.Equal("Zoe", result.Edit.Name);
        Assert.Equal("Science", result.Edit.Department);
        Assert.Equal("zoe@example.com", result.Edit.Email);
        AssertNothingWritten(api);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(null)]
    public async Task Edit_OfARowThatDoesNotExist_ChangesNothing(int? rowIndex)
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Edit, rowIndex));

        Assert.Null(result.EditIndex);
        Assert.False(result.Rows[0].IsEditing);
    }

    [Fact]
    public async Task Update_Valid_AppliesTheTrimmedValuesToThePendingRowOnly()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"));
        model.EditIndex = 0;
        model.Edit = new ExternalTestConsultantEditViewModel { Name = " Alan B ", Department = " Virology ", Email = " alanb@example.com " };

        var result = PageModel(await Post(controller, model, PendingListCommands.Update, 0));

        var row = Assert.Single(result.Rows);
        Assert.Equal("Alan B", row.Name);
        Assert.Equal("Virology", row.Department);
        Assert.Equal("alanb@example.com", row.Email);
        Assert.Null(result.EditIndex);
        Assert.False(row.IsEditing);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Theory]
    [InlineData("", "a@example.com", "Edit.Name", "Enter a name")]
    [InlineData("Alan", "", "Edit.Email", "Enter an email address")]
    [InlineData("Alan", "nope", "Edit.Email", "Enter an email address in the correct format")]
    public async Task Update_Invalid_KeepsTheRowInEditModeAndReportsTheFieldError(string name, string email, string key, string message)
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"));
        model.EditIndex = 0;
        model.Edit = new ExternalTestConsultantEditViewModel { Name = name, Email = email };

        var result = PageModel(await Post(controller, model, PendingListCommands.Update, 0));

        Assert.Equal(0, result.EditIndex);
        Assert.True(result.Rows[0].IsEditing);
        Assert.Equal("Alan", result.Rows[0].Name);
        Assert.Equal(message, controller.ModelState[key]!.Errors[0].ErrorMessage);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task CancelEdit_LeavesEditModeKeepingOtherPendingChanges()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan"), Row(null, "Grace"));
        model.EditIndex = 0;
        model.Edit = new ExternalTestConsultantEditViewModel { Name = "Changed", Email = "c@example.com" };

        var result = PageModel(await Post(controller, model, PendingListCommands.CancelEdit, 0));

        Assert.Null(result.EditIndex);
        Assert.Equal(["Alan", "Grace"], result.Rows.Select(r => r.Name));
    }

    [Fact]
    public async Task Add_Valid_AppendsAPendingRowAndResetsTheAddFormWithoutCallingTheApi()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"));
        model.Add = new ExternalTestConsultantAddViewModel { Name = " Grace ", Department = " Virology ", Email = " grace@example.com " };

        var result = PageModel(await Post(controller, model, PendingListCommands.Add));

        Assert.Equal(2, result.Rows.Count);
        var added = result.Rows[1];
        Assert.True(added.IsNew);
        Assert.Equal("Grace", added.Name);
        Assert.Equal("Virology", added.Department);
        Assert.Equal("grace@example.com", added.Email);
        Assert.Equal(string.Empty, result.Add.Name);
        AssertNothingWritten(api);
    }

    [Theory]
    [InlineData("", "grace@example.com", "Add.Name", "Enter a name")]
    [InlineData("Grace", "", "Add.Email", "Enter an email address")]
    [InlineData("Grace", "nope", "Add.Email", "Enter an email address in the correct format")]
    public async Task Add_Invalid_DoesNotAppendARowAndReportsTheFieldError(string name, string email, string key, string message)
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"));
        model.Add = new ExternalTestConsultantAddViewModel { Name = name, Email = email };

        var result = PageModel(await Post(controller, model, PendingListCommands.Add));

        Assert.Single(result.Rows);
        Assert.Equal(message, controller.ModelState[key]!.Errors[0].ErrorMessage);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Submit_WithNoRowInEditMode_BehavesAsAdd()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan"));
        model.Add = new ExternalTestConsultantAddViewModel { Name = "Grace", Email = "grace@example.com" };

        var result = PageModel(await Post(controller, model, PendingListCommands.Submit));

        Assert.Equal(["Alan", "Grace"], result.Rows.Select(r => r.Name));
    }

    [Fact]
    public async Task Submit_WithARowInEditMode_BehavesAsUpdateOfThatRow()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"), Row(IdB, "Zoe"));
        model.EditIndex = 1;
        model.Edit = new ExternalTestConsultantEditViewModel { Name = "Zoe Q", Email = "zoeq@example.com" };
        model.Add = new ExternalTestConsultantAddViewModel { Name = "Ignored", Email = "ignored@example.com" };

        var result = PageModel(await Post(controller, model, PendingListCommands.Submit));

        Assert.Equal(["Alan", "Zoe Q"], result.Rows.Select(r => r.Name));
        Assert.Null(result.EditIndex);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Remove_OfAPersistedConsultant_OnlyMarksItRemovedAndDeletesNothing()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"), Persisted(IdB, "Zoe"));
        var model = Page(Row(IdA, "Alan"), Row(IdB, "Zoe"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Remove, 0));

        Assert.True(result.Rows[0].IsRemoved);
        Assert.False(result.Rows[1].IsRemoved);
        Assert.Null(result.Message);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Remove_OfAConsultantAddedOnThePage_DropsTheRowAndKeepsTheEditRowAligned()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(null, "Grace"), Row(IdA, "Alan"));
        model.EditIndex = 1;
        model.Edit = new ExternalTestConsultantEditViewModel { Name = "Alan", Email = "alan@example.com" };

        var result = PageModel(await Post(controller, model, PendingListCommands.Remove, 0));

        Assert.Equal(["Alan"], result.Rows.Select(r => r.Name));
        Assert.Equal(0, result.EditIndex);
        Assert.True(result.Rows[0].IsEditing);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Remove_OfTheRowInEditMode_LeavesEditMode()
    {
        var (controller, _) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(IdA, "Alan"));
        model.EditIndex = 0;

        var result = PageModel(await Post(controller, model, PendingListCommands.Remove, 0));

        Assert.True(result.Rows[0].IsRemoved);
        Assert.Null(result.EditIndex);
    }

    [Fact]
    public async Task Remove_OfAnotherRowWhileEditing_KeepsTheEditRow()
    {
        var (controller, _) = Arrange(Persisted(IdA, "Alan"), Persisted(IdB, "Zoe"));
        var model = Page(Row(IdA, "Alan"), Row(IdB, "Zoe"));
        model.EditIndex = 1;

        var result = PageModel(await Post(controller, model, PendingListCommands.Remove, 0));

        Assert.True(result.Rows[0].IsRemoved);
        Assert.Equal(1, result.EditIndex);
    }

    [Fact]
    public async Task Remove_OfAConsultantAllocatedToSchemes_IsBlockedWithTheSchemesListedAndNothingChanges()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", allocations: 2, schemes: "Scheme A, Scheme B"));
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Remove, 0));

        Assert.False(result.Rows[0].IsRemoved);
        Assert.True(result.MessageIsError);
        Assert.Equal(
            "This external test consultant cannot be removed because it is being used on 2 scheme(s): Scheme A, Scheme B. Remove it from those schemes first.",
            result.Message);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Remove_OfAConsultantAllocatedToSchemesWithNoSchemeNames_StillBlocksWithTheCount()
    {
        var (controller, _) = Arrange(Persisted(IdA, "Alan", allocations: 1));
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Remove, 0));

        Assert.False(result.Rows[0].IsRemoved);
        Assert.Equal("This external test consultant cannot be removed because it is being used on 1 scheme(s).", result.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(null)]
    public async Task Remove_OfARowThatDoesNotExist_ChangesNothing(int? rowIndex)
    {
        var (controller, _) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Remove, rowIndex));

        Assert.False(result.Rows[0].IsRemoved);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task Toggle_FromActive_SetsInactiveWithTodaysDateInThePendingListOnly()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Toggle, 0));

        Assert.True(result.Rows[0].IsInactive);
        Assert.NotNull(result.Rows[0].InactiveDate);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Toggle_FromInactive_SetsActiveAndClearsTheDate()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", isInactive: true));
        var model = Page(Row(IdA, "Alan", isInactive: true));
        model.Rows[0].InactiveDate = new DateTime(2026, 1, 2);

        var result = PageModel(await Post(controller, model, PendingListCommands.Toggle, 0));

        Assert.False(result.Rows[0].IsInactive);
        Assert.Null(result.Rows[0].InactiveDate);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Toggle_OfAConsultantAddedOnThePage_FlipsTheStatusWithoutAnApiLookup()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(null, "Grace"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Toggle, 0));

        Assert.True(result.Rows[0].IsInactive);
    }

    [Fact]
    public async Task Toggle_OfAConsultantAllocatedToSchemes_IsBlockedWithTheSchemesListed()
    {
        var (controller, _) = Arrange(Persisted(IdA, "Alan", allocations: 1, schemes: "Scheme A"));
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Toggle, 0));

        Assert.False(result.Rows[0].IsInactive);
        Assert.True(result.MessageIsError);
        Assert.Equal(
            "This external test consultant cannot have its Active/Inactive status changed because it is being used on 1 scheme(s): Scheme A. Remove it from those schemes first.",
            result.Message);
    }

    [Fact]
    public async Task Toggle_OfARowThatDoesNotExist_ChangesNothing()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Toggle, 9));

        Assert.False(result.Rows[0].IsInactive);
    }

    [Fact]
    public async Task Save_WritesNewChangedRemovedAndStatusChangesThenNotifiesAndLeavesThePage()
    {
        var idC = Guid.NewGuid();
        var (controller, api) = Arrange(Persisted(IdA, "Alan"), Persisted(IdB, "Zoe"), Persisted(idC, "Carl"));
        api.SaveResult = new ExternalTestConsultantSaveResult(true, Persisted(Guid.NewGuid(), "Grace"), new Dictionary<string, string[]>());
        var changed = Row(IdB, "Zoe");
        changed.Email = "zoe.new@example.com";
        var model = Page(
            Row(IdA, "Alan", isInactive: true),
            changed,
            Row(idC, "Carl"),
            Row(null, "Grace"));
        model.Rows[2].IsRemoved = true;

        var result = await Post(controller, model, PendingListCommands.Save);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        Assert.Equal([idC], api.DeleteCalls);
        Assert.Equal(new ExternalTestConsultantSaveRequest("Grace", "Science", "grace@example.com"), api.LastCreateRequest);
        var update = Assert.Single(api.UpdateCalls);
        Assert.Equal(IdB, update.ExternalTestConsultantId);
        Assert.Equal("zoe.new@example.com", update.Request.Email);
        Assert.Equal([(IdA, true)], api.SetStatusCalls);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("External test consultants saved successfully.", notification.Message);
    }

    [Fact]
    public async Task Save_WithNothingChanged_WritesNothingButStillLeavesThePageWithTheNotification()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(IdA, "Alan"));

        var result = await Post(controller, model, PendingListCommands.Save);

        Assert.IsType<RedirectToActionResult>(result);
        AssertNothingWritten(api);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task Save_OfARemovedRowThatWasNeverSaved_JustDropsItWithoutCallingTheApi()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(null, "Grace"));
        model.Rows[0].IsRemoved = true;

        var result = await Post(controller, model, PendingListCommands.Save);

        Assert.IsType<RedirectToActionResult>(result);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Save_WhenTheApiRefusesTheDelete_StaysOnThePageWithTheMessageAndNoNotification()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        api.DeleteResult = new ExternalTestConsultantDeleteResponse(false, "This external test consultant is being used on: 1 scheme(s).");
        var model = Page(Row(IdA, "Alan"));
        model.Rows[0].IsRemoved = true;

        var result = PageModel(await Post(controller, model, PendingListCommands.Save));

        Assert.True(result.Rows[0].IsRemoved);
        Assert.Equal("Alan: This external test consultant is being used on: 1 scheme(s).", controller.ModelState["Rows"]!.Errors[0].ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Save_WhenTheApiRefusesTheDeleteWithNoMessage_UsesADefaultMessage()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        api.DeleteResult = new ExternalTestConsultantDeleteResponse(false, null);
        var model = Page(Row(IdA, "Alan"));
        model.Rows[0].IsRemoved = true;

        PageModel(await Post(controller, model, PendingListCommands.Save));

        Assert.Equal("Alan: The external test consultant could not be removed.", controller.ModelState["Rows"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Save_WhenTheApiRejectsAnUpdate_StaysOnThePageWithTheRowNameAndMessageAndSkipsItsStatusChange()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        api.SaveResult = new ExternalTestConsultantSaveResult(false, null, new Dictionary<string, string[]> { ["Email"] = ["Enter an email address"] });
        var row = Row(IdA, "Alan", isInactive: true);
        row.Email = string.Empty;
        var model = Page(row);

        var result = PageModel(await Post(controller, model, PendingListCommands.Save));

        Assert.Single(result.Rows);
        Assert.Equal("Alan: Enter an email address", controller.ModelState["Rows"]!.Errors[0].ErrorMessage);
        Assert.Empty(api.SetStatusCalls);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Save_WhenTheApiRejectsWithNoFieldErrors_UsesADefaultMessage()
    {
        var (controller, api) = Arrange();
        api.SaveResult = new ExternalTestConsultantSaveResult(false, null, new Dictionary<string, string[]>());
        var model = Page(Row(null, "Grace"));

        PageModel(await Post(controller, model, PendingListCommands.Save));

        Assert.Equal("Grace: The external test consultant could not be saved.", controller.ModelState["Rows"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Save_WhenTheApiRefusesTheStatusChange_StaysOnThePageWithTheMessage()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        api.SetStatusResult = new ExternalTestConsultantSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This external test consultant is being used on: 1 scheme(s)."] });
        var model = Page(Row(IdA, "Alan", isInactive: true));

        PageModel(await Post(controller, model, PendingListCommands.Save));

        Assert.Equal("Alan: This external test consultant is being used on: 1 scheme(s).", controller.ModelState["Rows"]!.Errors[0].ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Save_OfANewInactiveConsultant_CreatesItThenSetsItInactive()
    {
        var newId = Guid.NewGuid();
        var (controller, api) = Arrange();
        api.SaveResult = new ExternalTestConsultantSaveResult(true, Persisted(newId, "Grace"), new Dictionary<string, string[]>());
        var model = Page(Row(null, "Grace", isInactive: true));

        var result = await Post(controller, model, PendingListCommands.Save);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal([(newId, true)], api.SetStatusCalls);
    }

    [Fact]
    public async Task Save_WhileARowIsInEditMode_DoesNotApplyTheTypedEditValues()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(IdA, "Alan"));
        model.EditIndex = 0;
        model.Edit = new ExternalTestConsultantEditViewModel { Name = "Typed", Email = "typed@example.com" };

        var result = await Post(controller, model, PendingListCommands.Save);

        Assert.IsType<RedirectToActionResult>(result);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Apply_WritesThePendingRowsAndStaysOnThePageWithTheNotification()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(IdA, "Alan", isInactive: true));

        var result = PageModel(await Post(controller, model, PendingListCommands.Apply));

        Assert.Equal([(IdA, true)], api.SetStatusCalls);
        Assert.Equal("External test consultants saved successfully.", controller.TempData.GetNotification()!.Message);
        Assert.Single(result.Rows);
    }

    [Fact]
    public async Task Cancel_DiscardsEverythingAndRedirectsToSystemAdministrationWithoutWriting()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(IdA, "Alan", isInactive: true), Row(null, "Grace"));
        model.Rows[0].IsRemoved = true;

        var result = await Post(controller, model, PendingListCommands.Cancel);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task UnknownCommand_RedisplaysThePageUnchanged()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, command: null));

        Assert.Single(result.Rows);
        AssertNothingWritten(api);
    }
}
