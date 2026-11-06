using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.AuditReason;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;
using SchemeController = PTL.InternalWeb.Features.Scheme.SchemeController;
using SchemeFormViewModel = PTL.InternalWeb.Features.Scheme.SchemeFormViewModel;

namespace PTL.InternalWeb.Tests.Features.Scheme;

// Apply Button - Legacy Parity. Legacy Scheme.aspx has Apply beside Save on every tab: both save, but
// Save returns to the Scheme List and Apply stays on the scheme. The Previous Year page's own Apply
// action is a separate thing and is covered by SchemeControllerTests.PreviousYear.
public partial class SchemeControllerTests
{
    private const string CreatedMessage = "Scheme created successfully.";

    private static SchemeFormViewModel CommandModel() => new()
    {
        YearId = 2027,
        Identifier = "PT1234",
        Name = "Test Scheme",
    };

    private static FakeSchemeApiClient SavingApiClient(Guid schemeId) => new()
    {
        SaveResult = new SchemeSaveResult(true, SampleScheme(schemeId, Guid.NewGuid()), new Dictionary<string, string[]>())
    };

    private static void AssertStaysOnTheScheme(IActionResult result, Guid schemeId)
    {
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SchemeController.Edit), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Equal(schemeId, Assert.IsType<Guid>(redirect.RouteValues!["id"]));
    }

    private static void AssertReturnsToTheSchemeList(IActionResult result)
    {
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Null(redirect.RouteValues);
    }

    // ---- Edit ----

    [Theory]
    [InlineData("apply")]
    [InlineData("APPLY")]
    public async Task Edit_Post_Apply_SavesAndStaysOnTheScheme(string command)
    {
        var schemeId = Guid.NewGuid();
        var apiClient = SavingApiClient(schemeId);
        var controller = PostingController(apiClient);

        var result = await controller.Edit(schemeId, CommandModel(), command: command, cancellationToken: CancellationToken.None);

        AssertStaysOnTheScheme(result, schemeId);
        Assert.Equal("Test Scheme", apiClient.LastUpdateRequest!.Name);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal(UpdatedMessage, notification.Message);
    }

    // Regression: Save, and a post with no command at all, still leave for the Scheme List.
    [Theory]
    [InlineData("save")]
    [InlineData(null)]
    [InlineData("")]
    public async Task Edit_Post_SaveOrNoCommand_StillReturnsToTheSchemeList(string? command)
    {
        var schemeId = Guid.NewGuid();
        var apiClient = SavingApiClient(schemeId);
        var controller = PostingController(apiClient);

        var result = await controller.Edit(schemeId, CommandModel(), command: command, cancellationToken: CancellationToken.None);

        AssertReturnsToTheSchemeList(result);
        Assert.NotNull(apiClient.LastUpdateRequest);
        Assert.Equal(UpdatedMessage, controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task Edit_Post_ApplyWithAnInvalidForm_RedisplaysTheFormWithoutCallingTheApi()
    {
        var apiClient = new FakeSchemeApiClient();
        var controller = PostingController(apiClient);
        controller.ModelState.AddModelError(nameof(SchemeFormViewModel.Identifier), "Enter an identifier");
        var model = CommandModel();

        var result = await controller.Edit(Guid.NewGuid(), model, command: "apply", cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        Assert.Same(model, view.Model);
        Assert.Null(apiClient.LastUpdateRequest);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Edit_Post_ApplyRejectedByTheApi_RedisplaysTheFormWithTheErrors()
    {
        var apiClient = new FakeSchemeApiClient
        {
            SaveResult = new SchemeSaveResult(false, null, new Dictionary<string, string[]> { ["Identifier"] = ["Identifier is required"] })
        };
        var controller = PostingController(apiClient);
        var model = CommandModel();

        var result = await controller.Edit(Guid.NewGuid(), model, command: "apply", cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.Equal("Identifier is required", Assert.Single(controller.ModelState["Identifier"]!.Errors).ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Edit_Post_ApplyNeedingAnAuditReason_ShowsTheAuditPageCarryingTheApplyCommand()
    {
        var controller = PostingController(
            new FakeSchemeApiClient { SaveResult = AuditRequired() },
            ("__RequestVerificationToken", "token"),
            ("IsAuditStep", "false"),
            ("AuditWhy", string.Empty),
            ("command", "apply"));

        var result = await controller.Edit(Guid.NewGuid(), CommandModel(), command: "apply", cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(AuditReasonFlow.ViewName, view.ViewName);
        Assert.Equal(AuditObjectName, Assert.IsType<AuditReasonPageViewModel>(view.Model).AuditObjectName);

        // The audit page re-posts the fields it was given, so Apply survives the detour.
        Assert.Equal([new("command", "apply")], Assert.IsType<AuditReasonPageViewModel>(view.Model).Fields);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Edit_Post_AuditReasonSuppliedForAnApply_SavesAndStaysOnTheScheme()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = SavingApiClient(schemeId);
        var controller = PostingController(apiClient);

        var result = await controller.Edit(
            schemeId, AuditModel(isAuditStep: true, auditWhy: "Reordered the tests"), command: "apply", cancellationToken: CancellationToken.None);

        AssertStaysOnTheScheme(result, schemeId);
        Assert.Equal("Reordered the tests", apiClient.LastUpdateRequest!.AuditWhy);
        Assert.Equal(UpdatedMessage, controller.TempData.GetNotification()!.Message);
    }

    // Apply is not a save command on its own: a test-tree button still only restages the form.
    [Fact]
    public async Task Edit_Post_ApplyAlongsideATestCommand_RedisplaysWithoutSaving()
    {
        var apiClient = new FakeSchemeApiClient();
        var controller = PostingController(apiClient);
        var model = CommandModel();
        model.Tests.Add(new PTL.InternalWeb.Features.Scheme.SchemeTestViewModel { TestTypeId = Guid.NewGuid(), TestType = "Serology" });

        var result = await controller.Edit(
            Guid.NewGuid(), model, testCommand: "remove-test:0", confirmation: PTL.InternalWeb.Confirmation.ConfirmationFlow.Confirmed, command: "apply", cancellationToken: CancellationToken.None);

        Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Tests);
        Assert.Null(apiClient.LastUpdateRequest);
    }

    // ---- Create ----

    // Deliberate deviation from legacy, which re-displayed the Create page and inserted a second scheme
    // on the next Apply: the scheme now exists, so "stay on the scheme" means its Edit page.
    [Theory]
    [InlineData("apply")]
    [InlineData("APPLY")]
    public async Task Create_Post_Apply_SavesAndOpensTheNewSchemeForEditing(string command)
    {
        var createdId = Guid.NewGuid();
        var apiClient = SavingApiClient(createdId);
        var controller = PostingController(apiClient);

        var result = await controller.Create(CommandModel(), command: command, cancellationToken: CancellationToken.None);

        AssertStaysOnTheScheme(result, createdId);
        Assert.Equal("Test Scheme", apiClient.LastCreateRequest!.Name);
        Assert.Null(apiClient.LastUpdateRequest);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal(CreatedMessage, notification.Message);
    }

    [Theory]
    [InlineData("save")]
    [InlineData(null)]
    [InlineData("")]
    public async Task Create_Post_SaveOrNoCommand_StillReturnsToTheSchemeList(string? command)
    {
        var apiClient = SavingApiClient(Guid.NewGuid());
        var controller = PostingController(apiClient);

        var result = await controller.Create(CommandModel(), command: command, cancellationToken: CancellationToken.None);

        AssertReturnsToTheSchemeList(result);
        Assert.NotNull(apiClient.LastCreateRequest);
        Assert.Equal(CreatedMessage, controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task Create_Post_ApplyWithAnInvalidForm_RedisplaysTheFormWithoutCallingTheApi()
    {
        var apiClient = new FakeSchemeApiClient();
        var controller = PostingController(apiClient);
        controller.ModelState.AddModelError(nameof(SchemeFormViewModel.Identifier), "Enter an identifier");
        var model = CommandModel();

        var result = await controller.Create(model, command: "apply", cancellationToken: CancellationToken.None);

        Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
        Assert.Null(apiClient.LastCreateRequest);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Create_Post_ApplyRejectedByTheApi_RedisplaysTheFormWithTheErrors()
    {
        var apiClient = new FakeSchemeApiClient
        {
            SaveResult = new SchemeSaveResult(false, null, new Dictionary<string, string[]> { ["Identifier"] = ["Identifier is required"] })
        };
        var controller = PostingController(apiClient);
        var model = CommandModel();

        var result = await controller.Create(model, command: "apply", cancellationToken: CancellationToken.None);

        Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Identifier is required", Assert.Single(controller.ModelState["Identifier"]!.Errors).ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    // Nothing to stay on when the API reports success without a scheme, so Apply redisplays like Save does.
    [Fact]
    public async Task Create_Post_ApplyWithoutASchemeInTheResponse_RedisplaysTheFormWithTheGenericError()
    {
        var controller = PostingController(new FakeSchemeApiClient { SaveResult = new SchemeSaveResult(true, null, new Dictionary<string, string[]>()) });
        var model = CommandModel();

        var result = await controller.Create(model, command: "apply", cancellationToken: CancellationToken.None);

        Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
        Assert.Contains(
            controller.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage),
            message => message == "The scheme could not be created.");
        Assert.Null(controller.TempData.GetNotification());
    }
}
