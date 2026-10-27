using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.User;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

// Internal Test Consultant Department (legacy TCDepartment.aspx): row edit/update/cancel and the Status toggle only
// change the pending list; page Save writes it; Cancel discards it. The page has no Add, Remove or Apply.
public class InternalTestConsultantDepartmentControllerTests
{
    private static readonly Guid IdA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid IdB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly DateTime StoredInactiveDate = new(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);

    private static UserResponse Persisted(Guid id, string name, string department = "Science", bool isInactive = false, int allocations = 0, string? schemes = null) =>
        new(id, $"m{name.ToLowerInvariant()}", name, name, "Smith", $"{name.ToLowerInvariant()}@apha.gov.uk", department, isInactive, isInactive ? StoredInactiveDate : null, allocations, schemes);

    private static InternalTestConsultantRowViewModel Row(Guid? id, string name, string department = "Science", bool isInactive = false) =>
        new() { UserId = id, Username = $"m{name.ToLowerInvariant()}", FriendlyName = name, Department = department, IsInactive = isInactive, InactiveDate = isInactive ? StoredInactiveDate : null };

    private static InternalTestConsultantDepartmentViewModel Page(params InternalTestConsultantRowViewModel[] rows) => new() { Rows = [.. rows] };

    private static (SystemAdministrationController Controller, FakeUserApiClient Api) Arrange(params UserResponse[] consultants)
    {
        var api = new FakeUserApiClient { TestConsultants = consultants };
        return (SystemAdministrationControllerFactory.Create(userApiClient: api), api);
    }

    private static InternalTestConsultantDepartmentViewModel PageModel(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.InternalTestConsultantDepartment), view.ViewName);
        return Assert.IsType<InternalTestConsultantDepartmentViewModel>(view.Model);
    }

    private static Task<IActionResult> Post(SystemAdministrationController controller, InternalTestConsultantDepartmentViewModel model, string? command, int? rowIndex = null) =>
        controller.InternalTestConsultantDepartment(model, command, rowIndex, CancellationToken.None);

    [Fact]
    public async Task Get_BuildsRowsSortedByNameIgnoringCase()
    {
        var (controller, _) = Arrange(Persisted(IdA, "zoe"), Persisted(IdB, "Alan", "Virology", isInactive: true));

        var model = PageModel(await controller.InternalTestConsultantDepartment(CancellationToken.None));

        Assert.Equal(["Alan", "zoe"], model.Rows.Select(r => r.FriendlyName));
        Assert.Equal(IdB, model.Rows[0].UserId);
        Assert.Equal("Virology", model.Rows[0].Department);
        Assert.True(model.Rows[0].IsInactive);
        Assert.Equal(StoredInactiveDate, model.Rows[0].InactiveDate);
        Assert.All(model.Rows, r => Assert.False(r.IsEditing));
        Assert.Null(model.EditIndex);
    }

    [Fact]
    public async Task Edit_PutsTheRowInEditModeWithItsCurrentDepartmentAndWritesNothing()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"), Row(IdB, "Zoe", "Virology"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Edit, 1));

        Assert.Equal(1, result.EditIndex);
        Assert.True(result.Rows[1].IsEditing);
        Assert.False(result.Rows[0].IsEditing);
        Assert.Equal("Virology", result.Edit.Department);
        Assert.Empty(api.UpdateTestConsultantCalls);
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
    }

    [Fact]
    public async Task Update_Valid_AppliesTheTrimmedDepartmentToThePendingRowOnly()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"));
        model.EditIndex = 0;
        model.Edit = new InternalTestConsultantEditViewModel { Department = " Virology " };

        var result = PageModel(await Post(controller, model, PendingListCommands.Update, 0));

        Assert.Equal("Virology", result.Rows[0].Department);
        Assert.Null(result.EditIndex);
        Assert.False(result.Rows[0].IsEditing);
        Assert.Empty(api.UpdateTestConsultantCalls);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Update_WithADepartmentOverFiftyCharacters_KeepsTheRowInEditModeAndReportsTheError()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"));
        model.EditIndex = 0;
        model.Edit = new InternalTestConsultantEditViewModel { Department = new string('x', 51) };

        var result = PageModel(await Post(controller, model, PendingListCommands.Update, 0));

        Assert.Equal(0, result.EditIndex);
        Assert.Equal("Science", result.Rows[0].Department);
        Assert.Equal("Department must be 50 characters or fewer", controller.ModelState["Edit.Department"]!.Errors[0].ErrorMessage);
        Assert.Empty(api.UpdateTestConsultantCalls);
    }

    [Fact]
    public async Task Update_WithABlankDepartment_IsAllowed()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan"));
        model.EditIndex = 0;
        model.Edit = new InternalTestConsultantEditViewModel { Department = string.Empty };

        var result = PageModel(await Post(controller, model, PendingListCommands.Update, 0));

        Assert.Equal(string.Empty, result.Rows[0].Department);
        Assert.Null(result.EditIndex);
    }

    [Fact]
    public async Task CancelEdit_LeavesEditModeKeepingOtherPendingChanges()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan", "Changed"), Row(IdB, "Zoe"));
        model.EditIndex = 1;
        model.Edit = new InternalTestConsultantEditViewModel { Department = "Typed" };

        var result = PageModel(await Post(controller, model, PendingListCommands.CancelEdit, 1));

        Assert.Null(result.EditIndex);
        Assert.Equal(["Changed", "Science"], result.Rows.Select(r => r.Department));
    }

    [Fact]
    public async Task Submit_WithARowInEditMode_BehavesAsUpdateOfThatRow()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"), Row(IdB, "Zoe"));
        model.EditIndex = 1;
        model.Edit = new InternalTestConsultantEditViewModel { Department = "Virology" };

        var result = PageModel(await Post(controller, model, PendingListCommands.Submit));

        Assert.Equal("Virology", result.Rows[1].Department);
        Assert.Null(result.EditIndex);
        Assert.Empty(api.UpdateTestConsultantCalls);
    }

    [Fact]
    public async Task Submit_WithNoRowInEditMode_DoesNothing()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Submit));

        Assert.Equal("Science", result.Rows[0].Department);
        Assert.Empty(api.UpdateTestConsultantCalls);
    }

    [Fact]
    public async Task Toggle_FromActive_SetsInactiveWithTodaysDateInThePendingListOnly()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Toggle, 0));

        Assert.True(result.Rows[0].IsInactive);
        Assert.NotNull(result.Rows[0].InactiveDate);
        Assert.Null(result.Message);
        Assert.Empty(api.UpdateTestConsultantCalls);
    }

    [Fact]
    public async Task Toggle_FromInactive_SetsActiveAndClearsTheDateWithoutAConfirmationPrompt()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", isInactive: true));
        var model = Page(Row(IdA, "Alan", isInactive: true));

        var result = PageModel(await Post(controller, model, PendingListCommands.Toggle, 0));

        Assert.False(result.Rows[0].IsInactive);
        Assert.Null(result.Rows[0].InactiveDate);
        Assert.Empty(api.UpdateTestConsultantCalls);
    }

    [Fact]
    public async Task Toggle_OfAConsultantAllocatedToSchemes_IsBlockedWithTheSchemesListed()
    {
        var (controller, _) = Arrange(Persisted(IdA, "Alan", allocations: 2, schemes: "Scheme A, Scheme B"));
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Toggle, 0));

        Assert.False(result.Rows[0].IsInactive);
        Assert.True(result.MessageIsError);
        Assert.Equal(
            "This internal test consultant cannot have its Active/Inactive status changed because it is being used on 2 scheme(s): Scheme A, Scheme B. Remove it from those schemes first.",
            result.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(null)]
    public async Task Toggle_OfARowThatDoesNotExist_ChangesNothing(int? rowIndex)
    {
        var (controller, _) = Arrange();
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Toggle, rowIndex));

        Assert.False(result.Rows[0].IsInactive);
    }

    [Fact]
    public async Task Toggle_OfARowWithNoUserId_ChangesNothing()
    {
        var (controller, _) = Arrange();
        var model = Page(Row(null, "Alan"));

        var result = PageModel(await Post(controller, model, PendingListCommands.Toggle, 0));

        Assert.False(result.Rows[0].IsInactive);
    }

    [Fact]
    public async Task Save_WritesOnlyChangedRowsThenNotifiesAndLeavesThePage()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"), Persisted(IdB, "Zoe"));
        var model = Page(Row(IdA, "Alan"), Row(IdB, "Zoe", "Virology"));

        var result = await Post(controller, model, PendingListCommands.Save);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        var call = Assert.Single(api.UpdateTestConsultantCalls);
        Assert.Equal(IdB, call.UserId);
        Assert.Equal(new UpdateTestConsultantRequest("Virology", false, null), call.Request);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Internal test consultant departments saved successfully.", notification.Message);
    }

    [Fact]
    public async Task Save_OfANewlyInactiveConsultant_StampsTodaysDateRatherThanThePostedOne()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var posted = Row(IdA, "Alan", isInactive: true);
        posted.InactiveDate = new DateTime(1999, 1, 1);
        var before = DateTime.UtcNow;

        await Post(controller, Page(posted), PendingListCommands.Save);

        var request = Assert.Single(api.UpdateTestConsultantCalls).Request;
        Assert.True(request.IsInactive);
        Assert.InRange(request.InactiveDate!.Value, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task Save_OfAnAlreadyInactiveConsultantWhoseDepartmentChanged_KeepsTheStoredInactiveDate()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", isInactive: true));
        var model = Page(Row(IdA, "Alan", "Virology", isInactive: true));

        await Post(controller, model, PendingListCommands.Save);

        var request = Assert.Single(api.UpdateTestConsultantCalls).Request;
        Assert.Equal("Virology", request.Department);
        Assert.True(request.IsInactive);
        Assert.Equal(StoredInactiveDate, request.InactiveDate);
    }

    [Fact]
    public async Task Save_OfAReactivatedConsultant_ClearsTheInactiveDate()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan", isInactive: true));
        var model = Page(Row(IdA, "Alan"));

        await Post(controller, model, PendingListCommands.Save);

        var request = Assert.Single(api.UpdateTestConsultantCalls).Request;
        Assert.False(request.IsInactive);
        Assert.Null(request.InactiveDate);
    }

    [Fact]
    public async Task Save_IgnoresRowsThatAreNotStoredConsultants()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(null, "Ghost", "Virology"), Row(Guid.NewGuid(), "Unknown", "Virology"));

        var result = await Post(controller, model, PendingListCommands.Save);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(api.UpdateTestConsultantCalls);
    }

    [Fact]
    public async Task Save_WithNothingChanged_WritesNothingButStillLeavesThePageWithTheNotification()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(IdA, "Alan"));

        var result = await Post(controller, model, PendingListCommands.Save);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(api.UpdateTestConsultantCalls);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task Cancel_DiscardsEverythingAndRedirectsToSystemAdministrationWithoutWriting()
    {
        var (controller, api) = Arrange(Persisted(IdA, "Alan"));
        var model = Page(Row(IdA, "Alan", "Virology", isInactive: true));

        var result = await Post(controller, model, PendingListCommands.Cancel);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        Assert.Empty(api.UpdateTestConsultantCalls);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task UnknownCommand_RedisplaysThePageUnchanged()
    {
        var (controller, api) = Arrange();
        var model = Page(Row(IdA, "Alan"));

        var result = PageModel(await Post(controller, model, command: null));

        Assert.Single(result.Rows);
        Assert.Empty(api.UpdateTestConsultantCalls);
    }
}
