using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Country;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Navigation;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

// Manage Countries (legacy ManageCountries.aspx): row edit/update/cancel/add only change the pending list; page Save
// writes it then leaves; Cancel discards it; Remove marks the row removed and is blocked while in use.
public class CountryManagementControllerTests
{
    private const string Duplicate = "This country already exists. Please try with different name.";

    private static readonly Guid IdFrance = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid IdGermany = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid TypeEu = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TypeNonEu = Guid.Parse("22222222-0000-0000-0000-000000000002");

    private static CountryResponse Persisted(Guid id, string name, Guid typeId, int allocations = 0) =>
        new(id, name, typeId, typeId == TypeEu ? "EU" : "Non-EU", allocations);

    private static CountryManagementRowViewModel Row(Guid? id, string name, Guid typeId) =>
        new() { CountryId = id, Country = name, CountryTypeId = typeId };

    private static CountryManagementViewModel Page(params CountryManagementRowViewModel[] rows) => new() { Rows = [.. rows] };

    private static (SystemAdministrationController Controller, FakeCountryApiClient Api) Arrange(params CountryResponse[] countries)
    {
        var api = new FakeCountryApiClient
        {
            Countries = countries,
            CountryTypes = [new CountryTypeResponse(TypeNonEu, "Non-EU"), new CountryTypeResponse(TypeEu, "EU")]
        };
        return (SystemAdministrationControllerFactory.Create(countryApiClient: api), api);
    }

    private static CountryManagementViewModel PageModel(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.CountryManagement), view.ViewName);
        return Assert.IsType<CountryManagementViewModel>(view.Model);
    }

    private static void AssertNothingWritten(FakeCountryApiClient api)
    {
        Assert.Empty(api.CreateRequests);
        Assert.Empty(api.UpdateRequests);
        Assert.Empty(api.DeleteCalls);
    }

    [Fact]
    public async Task Get_BuildsRowsSortedByNameWithTypeNamesAndSortedTypeOptions()
    {
        var (controller, _) = Arrange(Persisted(IdGermany, "germany", TypeNonEu), Persisted(IdFrance, "France", TypeEu));

        var model = PageModel(await controller.CountryManagement(CancellationToken.None));

        Assert.Equal(["France", "germany"], model.Rows.Select(r => r.Country));
        Assert.Equal(["EU", "Non-EU"], model.Rows.Select(r => r.CountryType));
        Assert.Equal(IdFrance, model.Rows[0].CountryId);
        Assert.All(model.Rows, r => Assert.False(r.IsEditing));
        Assert.Null(model.EditIndex);
        Assert.Equal(["EU", "Non-EU"], model.CountryTypeOptions.Select(o => o.Text));
        Assert.Equal([TypeEu.ToString(), TypeNonEu.ToString()], model.CountryTypeOptions.Select(o => o.Value));
    }

    [Fact]
    public async Task Get_WithARowWhoseTypeIsUnknown_LeavesTheTypeNameBlank()
    {
        var (controller, _) = Arrange(Persisted(IdFrance, "France", Guid.NewGuid()));

        var model = PageModel(await controller.CountryManagement(CancellationToken.None));

        Assert.Equal(string.Empty, Assert.Single(model.Rows).CountryType);
    }

    [Fact]
    public async Task Edit_PutsTheRowInEditModeWithItsCurrentValuesAndWritesNothing()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu), Row(IdGermany, "Germany", TypeNonEu));

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Edit, 1, CancellationToken.None));

        Assert.Equal(1, result.EditIndex);
        Assert.Equal("Germany", result.Edit.Country);
        Assert.Equal(TypeNonEu, result.Edit.CountryTypeId);
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
        var model = Page(Row(IdFrance, "France", TypeEu), Row(IdGermany, "Germany", TypeNonEu));

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Edit, rowIndex, CancellationToken.None));

        Assert.Null(result.EditIndex);
        Assert.All(result.Rows, r => Assert.False(r.IsEditing));
    }

    [Fact]
    public async Task StaleEditIndexBeyondTheRows_IsClearedWhenThePageIsRedisplayed()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu));
        model.EditIndex = 7;

        var result = PageModel(await controller.CountryManagement(model, command: null, rowIndex: null, CancellationToken.None));

        Assert.Null(result.EditIndex);
    }

    [Fact]
    public async Task Update_Valid_ChangesThePendingRowOnlyAndLeavesEditMode()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu));
        var model = Page(Row(IdFrance, "France", TypeEu));
        model.EditIndex = 0;
        model.Edit = new CountryManagementEditViewModel { Country = "  French Republic ", CountryTypeId = TypeNonEu };

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Update, 0, CancellationToken.None));

        var row = Assert.Single(result.Rows);
        Assert.Equal("French Republic", row.Country);
        Assert.Equal(TypeNonEu, row.CountryTypeId);
        Assert.Equal("Non-EU", row.CountryType);
        Assert.Equal(IdFrance, row.CountryId);
        Assert.Null(result.EditIndex);
        Assert.False(row.IsEditing);
        Assert.Equal(string.Empty, result.Edit.Country);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Update_Invalid_KeepsTheRowInEditModeWithFieldErrorsAndLeavesTheRowUnchanged()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu));
        model.EditIndex = 0;
        model.Edit = new CountryManagementEditViewModel { Country = string.Empty, CountryTypeId = null };

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Update, 0, CancellationToken.None));

        Assert.Equal(0, result.EditIndex);
        Assert.True(result.Rows[0].IsEditing);
        Assert.Equal("France", result.Rows[0].Country);
        Assert.Equal(TypeEu, result.Rows[0].CountryTypeId);
        Assert.Equal("Enter a country name", controller.ModelState["Edit.Country"]!.Errors[0].ErrorMessage);
        Assert.Equal("Select a country type", controller.ModelState["Edit.CountryTypeId"]!.Errors[0].ErrorMessage);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task CancelEdit_LeavesEditModeAndKeepsEveryOtherPendingChange()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdFrance, "France (pending change)", TypeEu), Row(null, "Spain", TypeEu));
        model.EditIndex = 1;
        model.Edit = new CountryManagementEditViewModel { Country = "typed but abandoned", CountryTypeId = TypeNonEu };

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.CancelEdit, 1, CancellationToken.None));

        Assert.Null(result.EditIndex);
        Assert.Equal(["France (pending change)", "Spain"], result.Rows.Select(r => r.Country));
        Assert.All(result.Rows, r => Assert.False(r.IsEditing));
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Add_Valid_AppendsAPendingRowAndResetsTheAddFormWithoutCallingTheApi()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu));
        model.Add = new CountryManagementAddViewModel { Country = " Spain ", CountryTypeId = TypeNonEu };

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Add, null, CancellationToken.None));

        Assert.Equal(2, result.Rows.Count);
        var added = result.Rows[1];
        Assert.Null(added.CountryId);
        Assert.Equal("Spain", added.Country);
        Assert.Equal(TypeNonEu, added.CountryTypeId);
        Assert.Equal("Non-EU", added.CountryType);
        Assert.Equal(string.Empty, result.Add.Country);
        Assert.Null(result.Add.CountryTypeId);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Add_WithoutNameOrType_DoesNotAppendARowAndReportsBothFieldErrors()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu));

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Add, null, CancellationToken.None));

        Assert.Single(result.Rows);
        Assert.Equal("Enter a country name", controller.ModelState["Add.Country"]!.Errors[0].ErrorMessage);
        Assert.Equal("Select a country type", controller.ModelState["Add.CountryTypeId"]!.Errors[0].ErrorMessage);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Add_WithANameLongerThanFiftyCharacters_IsRejected()
    {
        var (controller, _) = Arrange();
        var model = Page();
        model.Add = new CountryManagementAddViewModel { Country = new string('x', 51), CountryTypeId = TypeEu };

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Add, null, CancellationToken.None));

        Assert.Empty(result.Rows);
        Assert.Equal("Country name must be 50 characters or fewer", controller.ModelState["Add.Country"]!.Errors[0].ErrorMessage);
    }

    [Theory]
    [InlineData("France")]
    [InlineData("FRANCE")]
    [InlineData("  france  ")]
    public async Task Add_WithANameAlreadyOnThePage_IsRejectedIgnoringCaseWithTheLegacyMessage(string name)
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu));
        model.Add = new CountryManagementAddViewModel { Country = name, CountryTypeId = TypeNonEu };

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Add, null, CancellationToken.None));

        Assert.Single(result.Rows);
        Assert.Equal(Duplicate, controller.ModelState["Add.Country"]!.Errors[0].ErrorMessage);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Add_WithANameDuplicatingAnUnsavedPendingRow_IsAlsoRejected()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(null, "Spain", TypeEu));
        model.Add = new CountryManagementAddViewModel { Country = "spain", CountryTypeId = TypeEu };

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Add, null, CancellationToken.None));

        Assert.Single(result.Rows);
        Assert.Equal(Duplicate, controller.ModelState["Add.Country"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Submit_WithNoRowInEditMode_BehavesAsAdd()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu));
        model.Add = new CountryManagementAddViewModel { Country = "Spain", CountryTypeId = TypeEu };

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Submit, null, CancellationToken.None));

        Assert.Equal(["France", "Spain"], result.Rows.Select(r => r.Country));
    }

    [Fact]
    public async Task Submit_WithARowInEditMode_BehavesAsUpdateOfThatRow()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu), Row(IdGermany, "Germany", TypeNonEu));
        model.EditIndex = 1;
        model.Edit = new CountryManagementEditViewModel { Country = "Federal Germany", CountryTypeId = TypeEu };
        model.Add = new CountryManagementAddViewModel { Country = "Ignored", CountryTypeId = TypeEu };

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Submit, null, CancellationToken.None));

        Assert.Equal(["France", "Federal Germany"], result.Rows.Select(r => r.Country));
        Assert.Null(result.EditIndex);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Save_WritesOnlyNewAndChangedRowsThenNotifiesAndLeavesThePage()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu), Persisted(IdGermany, "Germany", TypeNonEu));
        var model = Page(
            Row(IdFrance, "France", TypeEu),
            Row(IdGermany, "Germany", TypeEu),
            Row(null, "Spain", TypeEu));

        var result = await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        var update = Assert.Single(api.UpdateRequests);
        Assert.Equal(IdGermany, update.CountryId);
        Assert.Equal(new CountrySaveRequest("Germany", TypeEu), update.Request);
        Assert.Equal(new CountrySaveRequest("Spain", TypeEu), Assert.Single(api.CreateRequests));
        Assert.Empty(api.DeleteCalls);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Countries saved successfully.", notification.Message);
    }

    [Fact]
    public async Task Save_WithARenamedRow_UpdatesIt()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu));
        var model = Page(Row(IdFrance, "French Republic", TypeEu));

        await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.Equal("French Republic", Assert.Single(api.UpdateRequests).Request.Country);
    }

    [Fact]
    public async Task Save_WithNothingChanged_WritesNothingButStillLeavesThePageWithTheNotification()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu));
        var model = Page(Row(IdFrance, "France", TypeEu));

        var result = await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        AssertNothingWritten(api);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task Save_ForACountryNoLongerInTheDatabase_UpdatesItByIdRatherThanSkippingIt()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu));

        await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.Equal(IdFrance, Assert.Single(api.UpdateRequests).CountryId);
    }

    [Fact]
    public async Task Save_WhenTheApiRejectsARow_KeepsEveryRowPendingWithAPrefixedRowsErrorAndNoNotification()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu));
        var created = Persisted(Guid.NewGuid(), "Spain", TypeEu);
        api.SaveResultFor = request => request.Country == "Bad"
            ? new CountrySaveResult(false, null, new Dictionary<string, string[]> { ["Country"] = [Duplicate, "Second problem"] })
            : new CountrySaveResult(true, created, new Dictionary<string, string[]>());
        var model = Page(Row(IdFrance, "France", TypeEu), Row(null, "Bad", TypeEu), Row(null, "Spain", TypeEu));
        model.EditIndex = 1;

        var result = await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        var page = PageModel(result);
        Assert.Equal(["France", "Bad", "Spain"], page.Rows.Select(r => r.Country));
        Assert.Equal([$"Bad: {Duplicate}", "Bad: Second problem"], controller.ModelState["Rows"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Null(page.Rows[1].CountryId);
        Assert.Equal(created.CountryId, page.Rows[2].CountryId);
        Assert.Null(page.EditIndex);
        Assert.All(page.Rows, r => Assert.False(r.IsEditing));
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Cancel_LeavesForSystemAdministrationWithoutWritingOrNotifying()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(null, "Spain", TypeEu), Row(IdFrance, "Changed", TypeEu));

        var result = await controller.CountryManagement(model, PendingListCommands.Cancel, null, CancellationToken.None);

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
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu));
        var model = Page(Row(IdFrance, "France", TypeEu));

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Remove, rowIndex, CancellationToken.None));

        Assert.Single(result.Rows);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Remove_OfAnUnsavedRow_DropsItFromThePendingListWithoutCallingTheApiAndKeepsTheEditRowSelected()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(null, "Spain", TypeEu), Row(IdGermany, "Germany", TypeNonEu));
        model.EditIndex = 1;
        model.Edit = new CountryManagementEditViewModel { Country = "Federal Germany", CountryTypeId = TypeEu };

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Remove, 0, CancellationToken.None));

        var remaining = Assert.Single(result.Rows);
        Assert.Equal("Germany", remaining.Country);
        Assert.Equal(0, result.EditIndex);
        Assert.True(remaining.IsEditing);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Remove_OfTheUnsavedRowBeingEdited_LeavesEditMode()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdGermany, "Germany", TypeNonEu), Row(null, "Spain", TypeEu));
        model.EditIndex = 1;

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Remove, 1, CancellationToken.None));

        Assert.Single(result.Rows);
        Assert.Null(result.EditIndex);
    }

    [Fact]
    public async Task Remove_WhenInUse_IsBlockedWithTheLegacyDependencyMessage()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu, allocations: 8));
        var model = Page(Row(IdFrance, "France", TypeEu));

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Remove, 0, CancellationToken.None));

        Assert.True(result.MessageIsError);
        Assert.Equal("This country cannot be removed because it is being used by 8 customer(s)/participant(s)/Group Addresses.", result.Message);
        Assert.Single(result.Rows);
        Assert.False(result.Rows[0].IsRemoved);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Remove_MarksTheRowRemovedOnTheRedisplayedPageWithoutDeletingNotifyingOrRedirecting()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu), Persisted(IdGermany, "Germany", TypeNonEu));
        var model = Page(Row(IdFrance, "France", TypeEu), Row(IdGermany, "Germany", TypeNonEu));

        var result = await controller.CountryManagement(model, PendingListCommands.Remove, 0, CancellationToken.None);

        Assert.IsNotType<RedirectToActionResult>(result);
        var page = PageModel(result);
        Assert.Equal(2, page.Rows.Count);
        Assert.True(page.Rows[0].IsRemoved);
        Assert.Equal(IdFrance, page.Rows[0].CountryId);
        Assert.False(page.Rows[1].IsRemoved);
        Assert.Null(page.Message);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Remove_ForACountryTheApiNoLongerReturns_StillMarksItRemoved()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu));

        var page = PageModel(await controller.CountryManagement(model, PendingListCommands.Remove, 0, CancellationToken.None));

        Assert.True(Assert.Single(page.Rows).IsRemoved);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Remove_ForTheRowInEditMode_LeavesEditMode()
    {
        var (controller, _) = Arrange(Persisted(IdFrance, "France", TypeEu), Persisted(IdGermany, "Germany", TypeNonEu));
        var model = Page(Row(IdFrance, "France", TypeEu), Row(IdGermany, "Germany", TypeNonEu));
        model.EditIndex = 1;

        var page = PageModel(await controller.CountryManagement(model, PendingListCommands.Remove, 1, CancellationToken.None));

        Assert.True(page.Rows[1].IsRemoved);
        Assert.Null(page.EditIndex);
        Assert.All(page.Rows, r => Assert.False(r.IsEditing));
    }

    [Fact]
    public async Task Remove_ForADifferentRowThanTheOneInEditMode_KeepsEditMode()
    {
        var (controller, _) = Arrange(Persisted(IdFrance, "France", TypeEu), Persisted(IdGermany, "Germany", TypeNonEu));
        var model = Page(Row(IdFrance, "France", TypeEu), Row(IdGermany, "Germany", TypeNonEu));
        model.EditIndex = 1;

        var page = PageModel(await controller.CountryManagement(model, PendingListCommands.Remove, 0, CancellationToken.None));

        Assert.True(page.Rows[0].IsRemoved);
        Assert.Equal(1, page.EditIndex);
        Assert.True(page.Rows[1].IsEditing);
    }

    [Fact]
    public async Task Add_OfANameThatMatchesOnlyARemovedRow_IsAccepted()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu));
        model.Rows[0].IsRemoved = true;
        model.Add = new CountryManagementAddViewModel { Country = "france", CountryTypeId = TypeNonEu };

        var page = PageModel(await controller.CountryManagement(model, PendingListCommands.Add, null, CancellationToken.None));

        Assert.Equal(["France", "france"], page.Rows.Select(r => r.Country));
        Assert.True(page.Rows[0].IsRemoved);
        Assert.False(page.Rows[1].IsRemoved);
        Assert.False(controller.ModelState.ContainsKey("Add.Country"));
    }

    [Fact]
    public async Task Add_OfANameThatMatchesAnActiveRowEvenWhenAnotherRowOfThatNameIsRemoved_IsStillRejected()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu), Row(null, "France", TypeEu));
        model.Rows[0].IsRemoved = true;
        model.Add = new CountryManagementAddViewModel { Country = "France", CountryTypeId = TypeEu };

        var page = PageModel(await controller.CountryManagement(model, PendingListCommands.Add, null, CancellationToken.None));

        Assert.Equal(2, page.Rows.Count);
        Assert.Equal(Duplicate, controller.ModelState["Add.Country"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Save_DeletesEveryRemovedRowThenDropsItNotifiesAndLeavesThePage()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu), Persisted(IdGermany, "Germany", TypeNonEu));
        var model = Page(Row(IdFrance, "France", TypeEu), Row(IdGermany, "Germany", TypeNonEu));
        model.Rows[0].IsRemoved = true;
        model.EditIndex = 1;

        var result = await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.Equal(nameof(SystemAdministrationController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal([IdFrance], api.DeleteCalls);
        Assert.Equal(["Germany"], model.Rows.Select(r => r.Country));
        Assert.Null(model.EditIndex);
        Assert.Empty(api.CreateRequests);
        Assert.Empty(api.UpdateRequests);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Countries saved successfully.", notification.Message);
    }

    [Fact]
    public async Task Save_WritesNewAndChangedRowsButNeverAChangedRowThatIsRemoved()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu), Persisted(IdGermany, "Germany", TypeNonEu));
        var model = Page(Row(IdFrance, "France renamed", TypeEu), Row(IdGermany, "Germany", TypeEu), Row(null, "Spain", TypeEu));
        model.Rows[0].IsRemoved = true;

        await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.Equal([IdFrance], api.DeleteCalls);
        Assert.Equal(IdGermany, Assert.Single(api.UpdateRequests).CountryId);
        Assert.Equal("Spain", Assert.Single(api.CreateRequests).Country);
    }

    [Fact]
    public async Task Save_OfARemovedRowThatWasNeverSaved_JustDropsItWithoutCallingTheApi()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(null, "Spain", TypeEu));
        model.Rows[0].IsRemoved = true;

        var result = await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(model.Rows);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Save_WhenOneDeleteFails_KeepsThatRowRemovedWithItsNamedErrorAndDropsTheOthersWithNoNotification()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu), Persisted(IdGermany, "Germany", TypeNonEu));
        api.DeleteResultFor = id => id == IdGermany ? new CountryDeleteResponse(false, "Germany is in use.") : new CountryDeleteResponse(true, null);
        var model = Page(Row(IdFrance, "France", TypeEu), Row(IdGermany, "Germany", TypeNonEu), Row(null, "Spain", TypeEu));
        model.Rows[0].IsRemoved = true;
        model.Rows[1].IsRemoved = true;

        var result = await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None);

        var page = PageModel(result);
        Assert.Equal([IdFrance, IdGermany], api.DeleteCalls);
        Assert.Equal(["Germany", "Spain"], page.Rows.Select(r => r.Country));
        Assert.True(page.Rows[0].IsRemoved);
        Assert.False(page.Rows[1].IsRemoved);
        Assert.Equal(["Germany: Germany is in use."], controller.ModelState["Rows"]!.Errors.Select(e => e.ErrorMessage));
        Assert.Null(controller.TempData.GetNotification());
        Assert.Equal("Spain", Assert.Single(api.CreateRequests).Country);
    }

    [Fact]
    public async Task Save_WhenADeleteFailsWithoutAMessage_UsesTheDefaultMessage()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu));
        api.DeleteResponse = new CountryDeleteResponse(false, null);
        var model = Page(Row(IdFrance, "France", TypeEu));
        model.Rows[0].IsRemoved = true;

        var page = PageModel(await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None));

        Assert.True(Assert.Single(page.Rows).IsRemoved);
        Assert.Equal("France: The country could not be removed.", Assert.Single(controller.ModelState["Rows"]!.Errors).ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Save_WhenADeleteSucceedsButAWriteIsRejected_DropsTheDeletedRowAndStaysOnThePageWithoutNotification()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu));
        api.SaveResult = new CountrySaveResult(false, null, new Dictionary<string, string[]> { ["Country"] = ["Country already exists"] });
        var model = Page(Row(IdFrance, "France", TypeEu), Row(null, "Spain", TypeEu));
        model.Rows[0].IsRemoved = true;

        var page = PageModel(await controller.CountryManagement(model, PendingListCommands.Save, null, CancellationToken.None));

        Assert.Equal([IdFrance], api.DeleteCalls);
        Assert.Equal(["Spain"], page.Rows.Select(r => r.Country));
        Assert.Equal("Spain: Country already exists", Assert.Single(controller.ModelState["Rows"]!.Errors).ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Cancel_WithRemovedRowsPending_LeavesWithoutDeletingAnything()
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu));
        var model = Page(Row(IdFrance, "France", TypeEu));
        model.Rows[0].IsRemoved = true;

        var result = await controller.CountryManagement(model, PendingListCommands.Cancel, null, CancellationToken.None);

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
    [InlineData(PendingListCommands.Apply)]
    [InlineData(PendingListCommands.Submit)]
    public async Task RowCommands_WithARemovedRowPending_StayOnThePageAndNeverDelete(string command)
    {
        var (controller, api) = Arrange(Persisted(IdFrance, "France", TypeEu), Persisted(IdGermany, "Germany", TypeNonEu));
        var model = Page(Row(IdFrance, "France", TypeEu), Row(IdGermany, "Germany", TypeNonEu));
        model.Rows[0].IsRemoved = true;

        var result = await controller.CountryManagement(model, command, 1, CancellationToken.None);

        Assert.IsNotType<RedirectToActionResult>(result);
        Assert.True(model.Rows[0].IsRemoved);
        Assert.Empty(api.DeleteCalls);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task EditIndexPointingAtARemovedRow_IsClearedWhenThePageIsRedisplayed()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu), Row(IdGermany, "Germany", TypeNonEu));
        model.Rows[0].IsRemoved = true;
        model.EditIndex = 0;

        var page = PageModel(await controller.CountryManagement(model, command: null, rowIndex: null, CancellationToken.None));

        Assert.Null(page.EditIndex);
        Assert.All(page.Rows, r => Assert.False(r.IsEditing));
    }

    [Fact]
    public async Task UnknownCommand_RedisplaysThePendingPageUnchanged()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdFrance, "France", TypeEu));

        var result = PageModel(await controller.CountryManagement(model, "something-else", 0, CancellationToken.None));

        Assert.Single(result.Rows);
        AssertNothingWritten(api);
    }

    [Fact]
    public async Task Apply_IsNotACountryCommand_SoItChangesNothing()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(null, "Spain", TypeEu));

        var result = PageModel(await controller.CountryManagement(model, PendingListCommands.Apply, null, CancellationToken.None));

        Assert.Single(result.Rows);
        AssertNothingWritten(api);
        Assert.Null(controller.TempData.GetNotification());
    }
}
