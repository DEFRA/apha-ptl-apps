using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.Contracts.LabUsers;
using PTL.Contracts.LoginInstructions;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.LabUser;
using PTL.ExternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.ExternalWeb.Tests.Features.LabUser;

public class LabUserControllerTests
{
    private static readonly Guid LabId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PersonId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeLabUserApiClient _api = new();

    private LabUserController CreateController(bool withLab = true)
    {
        var controller = new LabUserController(_api);
        if (withLab)
        {
            AuthenticatedParticipant.Attach(controller, LabId);
        }
        else
        {
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        }

        controller.TempData = new TempDataDictionary(controller.HttpContext, new FakeTempDataProvider());
        return controller;
    }

    private static LabUserResponse Person(bool canChange = true, bool active = true) =>
        new(PersonId, "Sam Smith", "sam@lab.example", active, LoginInstructionsSent: true, canChange, HasSignedIn: true);

    private static LabUserInput Form(string name = "New Person", string email = "new@lab.example", bool active = true) =>
        new() { Name = name, Email = email, IsActive = active };

    [Fact]
    public void Controller_IsOnlyForThePrimaryContact()
    {
        var authorize = typeof(LabUserController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();

        Assert.Equal(ExternalPolicies.PrimaryContact, authorize.Policy);
    }

    // ---- no lab on the session: every action is a 404 and calls nothing ----

    [Fact]
    public async Task EveryAction_WithNoLabOnTheSession_IsNotFoundAndCallsNothing()
    {
        var controller = CreateController(withLab: false);

        Assert.IsType<NotFoundResult>(await controller.Index(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Add(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Add(Form(), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Edit(PersonId, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Edit(PersonId, Form(), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.SendInstructions(PersonId, CancellationToken.None));

        Assert.Empty(_api.ListCalls);
        Assert.Empty(_api.AddCalls);
        Assert.Empty(_api.UpdateCalls);
        Assert.Empty(_api.InstructionCalls);
    }

    [Fact]
    public async Task EveryAction_WhenTheApiDoesNotKnowTheLabOrPerson_IsNotFound()
    {
        _api.Found = false;
        var controller = CreateController();

        Assert.IsType<NotFoundResult>(await controller.Index(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Add(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Add(Form(), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Edit(PersonId, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Edit(PersonId, Form(), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.SendInstructions(PersonId, CancellationToken.None));
    }

    [Fact]
    public async Task Edit_Post_WhenTheUpdateFindsNoPerson_IsNotFound()
    {
        _api.Users.Add(Person());
        var controller = CreateController();

        // The person exists for the lookup but the save answers null (removed in between).
        var api = new NullUpdateApi(_api);
        var result = await new LabUserController(api) { ControllerContext = controller.ControllerContext, TempData = controller.TempData }
            .Edit(PersonId, Form(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---- the list ----

    [Fact]
    public async Task Index_ShowsTheLabsPeople_UsingTheLabOnTheSessionNotARequestValue()
    {
        _api.Users.Add(Person());
        var controller = CreateController();

        var model = Assert.IsType<LabUserListViewModel>(Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None)).Model);

        Assert.Equal("0142", model.LabCode);
        Assert.Equal(PersonId, Assert.Single(model.Users).LabUserId);
        Assert.Equal(LabId, Assert.Single(_api.ListCalls));
    }

    // ---- add ----

    [Fact]
    public async Task Add_Get_ShowsAnEmptyFormForTheLab()
    {
        var controller = CreateController();

        var model = Assert.IsType<LabUserFormViewModel>(Assert.IsType<ViewResult>(await controller.Add(CancellationToken.None)).Model);

        Assert.Equal(("0142", "Bristol Veterinary Diagnostics"), (model.LabCode, model.LabName));
        Assert.True(model.IsActive);
        Assert.False(model.IsEdit);
    }

    [Theory]
    [InlineData(LabUserSaveOutcome.Added, "Lab user added.")]
    [InlineData(LabUserSaveOutcome.AddedExistingAccount, "Lab user added. This person already had an account, so they can now sign in to this lab as well.")]
    public async Task Add_Post_Saved_ConfirmsAndGoesToTheList(LabUserSaveOutcome outcome, string message)
    {
        _api.SaveResult = LabUserSaveResult.Succeeded(outcome);
        var controller = CreateController();

        var result = await controller.Add(Form(" Pat ", " pat@lab.example ", active: true), CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal(message, notification.Message);
        var (participantId, request) = Assert.Single(_api.AddCalls);
        Assert.Equal(LabId, participantId);
        Assert.Equal(new LabUserSaveRequest(" Pat ", " pat@lab.example ", true), request);
    }

    [Fact]
    public async Task Add_Post_Rejected_ShowsTheFormAgainWithTheApisFieldErrorsAndWhatWasTyped()
    {
        _api.SaveResult = LabUserSaveResult.Failure("Email", "Enter a valid email address");
        var controller = CreateController();

        var result = await controller.Add(Form("Pat", "bad"), CancellationToken.None);

        var model = Assert.IsType<LabUserFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(("Pat", "bad"), (model.Name, model.Email));
        Assert.Equal("0142", model.LabCode);
        Assert.Equal("Enter a valid email address", controller.ModelState["Email"]!.Errors.Single().ErrorMessage);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Add_Post_EmptyFields_AreSentAsEmptyTextForTheApiToReport()
    {
        var controller = CreateController();

        await controller.Add(new LabUserInput(), CancellationToken.None);

        Assert.Equal(new LabUserSaveRequest(string.Empty, string.Empty, true), Assert.Single(_api.AddCalls).Request);
    }

    // ---- edit ----

    [Fact]
    public async Task Edit_Get_ShowsThePersonsCurrentDetails()
    {
        _api.Users.Add(Person());
        var controller = CreateController();

        var model = Assert.IsType<LabUserFormViewModel>(Assert.IsType<ViewResult>(await controller.Edit(PersonId, CancellationToken.None)).Model);

        Assert.Equal(PersonId, model.LabUserId);
        Assert.Equal(("Sam Smith", "sam@lab.example"), (model.Name, model.Email));
        Assert.True(model.IsEdit);
        Assert.True(model.CanChangeDetails);
        Assert.True(model.HasSignedIn);
        Assert.Equal("0142", model.LabCode);
    }

    [Fact]
    public async Task Edit_Get_PersonNotInThisLab_IsNotFound()
    {
        var controller = CreateController();

        Assert.IsType<NotFoundResult>(await controller.Edit(Guid.NewGuid(), CancellationToken.None));
    }

    [Theory]
    [InlineData(LabUserSaveOutcome.Updated, true, "Lab user updated.")]
    [InlineData(LabUserSaveOutcome.Updated, false, "Lab user updated. They can no longer sign in to this lab.")]
    [InlineData(LabUserSaveOutcome.UpdatedEmailChanged, true, "Lab user updated. The email address has changed, so they need to register or sign in with the new address, and you need to send them the login instructions again.")]
    public async Task Edit_Post_Saved_ConfirmsAccordingToWhatChanged(LabUserSaveOutcome outcome, bool active, string message)
    {
        _api.Users.Add(Person());
        _api.SaveResult = LabUserSaveResult.Succeeded(outcome);
        var controller = CreateController();

        var result = await controller.Edit(PersonId, Form("Sam S", "sam2@lab.example", active), CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(message, controller.TempData.GetNotification()!.Message);
        var call = Assert.Single(_api.UpdateCalls);
        Assert.Equal((LabId, PersonId), (call.ParticipantId, call.LabUserId));
        Assert.Equal(new LabUserSaveRequest("Sam S", "sam2@lab.example", active), call.Request);
    }

    // The form posts neither name nor email for a person shared with other labs, and a forged post must not change them either.
    [Fact]
    public async Task Edit_Post_SharedPerson_AlwaysSendsTheRecordedNameAndEmail()
    {
        _api.Users.Add(Person(canChange: false));
        var controller = CreateController();

        await controller.Edit(PersonId, Form("Forged", "forged@evil.example", active: false), CancellationToken.None);

        Assert.Equal(new LabUserSaveRequest("Sam Smith", "sam@lab.example", false), Assert.Single(_api.UpdateCalls).Request);
    }

    [Fact]
    public async Task Edit_Post_Rejected_ShowsTheFormAgainWithTheTypedValuesAndErrors()
    {
        _api.Users.Add(Person());
        _api.SaveResult = LabUserSaveResult.Failure("Email", "That email address is already used by another lab user");
        var controller = CreateController();

        var result = await controller.Edit(PersonId, Form("Typed", "typed@lab.example", active: false), CancellationToken.None);

        var model = Assert.IsType<LabUserFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(("Typed", "typed@lab.example", false), (model.Name, model.Email, model.IsActive));
        Assert.Equal(PersonId, model.LabUserId);
        Assert.True(model.HasSignedIn);
        Assert.Single(controller.ModelState["Email"]!.Errors);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Edit_Post_PersonNotInThisLab_IsNotFoundAndSavesNothing()
    {
        var controller = CreateController();

        Assert.IsType<NotFoundResult>(await controller.Edit(Guid.NewGuid(), Form(), CancellationToken.None));
        Assert.Empty(_api.UpdateCalls);
    }

    // ---- login instructions ----

    [Fact]
    public async Task SendInstructions_Sent_ConfirmsWithASuccessNotification()
    {
        var controller = CreateController();

        var result = await controller.SendInstructions(PersonId, CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal((NotificationType.Success, "Login instructions sent."), (notification!.Type, notification.Message));
        Assert.Equal((LabId, PersonId), Assert.Single(_api.InstructionCalls));
    }

    [Fact]
    public async Task SendInstructions_Refused_ShowsTheApisMessageAsAnError()
    {
        _api.InstructionsResponse = new LoginInstructionsResponse(false, "This lab user is not active.");
        var controller = CreateController();

        await controller.SendInstructions(PersonId, CancellationToken.None);

        var notification = controller.TempData.GetNotification();
        Assert.Equal((NotificationType.Error, "This lab user is not active."), (notification!.Type, notification.Message));
    }

    [Fact]
    public async Task SendInstructions_RefusedWithNoMessage_ShowsAGenericError()
    {
        _api.InstructionsResponse = new LoginInstructionsResponse(false);
        var controller = CreateController();

        await controller.SendInstructions(PersonId, CancellationToken.None);

        Assert.Equal("The login instructions email could not be sent. Please try again.", controller.TempData.GetNotification()!.Message);
    }

    // Answers the lookups from the wrapped fake but reports the person gone when the save is attempted.
    private sealed class NullUpdateApi(FakeLabUserApiClient inner) : PTL.ApiClient.ILabUserApiClient
    {
        public Task<LabUserListResponse?> GetListAsync(Guid participantId, CancellationToken cancellationToken = default) => inner.GetListAsync(participantId, cancellationToken);

        public Task<LabUserDetailsResponse?> GetAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default) => inner.GetAsync(participantId, labUserId, cancellationToken);

        public Task<LabUserSaveResult?> AddAsync(Guid participantId, LabUserSaveRequest request, CancellationToken cancellationToken = default) => inner.AddAsync(participantId, request, cancellationToken);

        public Task<LabUserSaveResult?> UpdateAsync(Guid participantId, Guid labUserId, LabUserSaveRequest request, CancellationToken cancellationToken = default) => Task.FromResult<LabUserSaveResult?>(null);

        public Task<LoginInstructionsResponse?> SendLoginInstructionsAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default) => inner.SendLoginInstructionsAsync(participantId, labUserId, cancellationToken);
    }
}
