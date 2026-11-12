using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.LoginInstructions;
using PTL.Contracts.TestConsultant;
using PTL.Contracts.Viewer;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

// The Login Instructions column of Manage Viewers and Manage External Test Consultants: Send Instructions emails the saved
// address through the API, an edited email address brings the link back, and the page takes its state from the database
// after Apply.
public class LoginInstructionsPendingListTests
{
    private static readonly Guid IdA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static ViewerResponse StoredViewer(bool sent, string email = "alan@example.com") => new(IdA, "Alan", email, [], [], sent);

    private static ViewerRowViewModel ViewerRow(Guid? id = null, string email = "alan@example.com", bool sent = false) =>
        new() { ViewerId = id ?? IdA, Name = "Alan", Email = email, LoginInstructionsSent = sent };

    private static ExternalTestConsultantResponse StoredConsultant(bool sent, string email = "alan@example.com") =>
        new(IdA, "Alan", "Science", email, false, null, 0, null, sent);

    private static ExternalTestConsultantRowViewModel ConsultantRow(Guid? id = null, string email = "alan@example.com", bool sent = false) =>
        new() { ExternalTestConsultantId = id ?? IdA, Name = "Alan", Department = "Science", Email = email, LoginInstructionsSent = sent };

    private static ViewerManagementViewModel ViewerPage(IActionResult result) =>
        Assert.IsType<ViewerManagementViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static ExternalTestConsultantManagementViewModel ConsultantPage(IActionResult result) =>
        Assert.IsType<ExternalTestConsultantManagementViewModel>(Assert.IsType<ViewResult>(result).Model);

    // ---- Viewers ----

    [Fact]
    public async Task Viewer_Get_ShowsTheStoredSentFlagOfEachRow()
    {
        var controller = SystemAdministrationControllerFactory.Create(viewerApiClient: new FakeViewerApiClient { Viewers = [StoredViewer(sent: true)] });

        var model = ViewerPage(await controller.ViewerManagement(CancellationToken.None));

        Assert.True(Assert.Single(model.Rows).LoginInstructionsSent);
    }

    [Fact]
    public async Task Viewer_SendInstructions_EmailsTheSavedViewerThenShowsItAsSent()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(viewerApiClient: new FakeViewerApiClient { Viewers = [StoredViewer(sent: false)] }, loginInstructionsApiClient: login);
        var model = new ViewerManagementViewModel { Rows = [ViewerRow()] };

        var result = ViewerPage(await controller.ViewerManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.Equal(IdA, Assert.Single(login.ViewerCalls));
        Assert.True(result.Rows[0].LoginInstructionsSent);
        Assert.Null(result.Message);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Login instructions sent to Alan.", notification.Message);
    }

    [Fact]
    public async Task Viewer_SendInstructions_KeepsOtherPendingChangesOnThePage()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(viewerApiClient: new FakeViewerApiClient { Viewers = [StoredViewer(sent: false)] }, loginInstructionsApiClient: login);
        var pending = new ViewerRowViewModel { ViewerId = null, Name = "New Person", Email = "new@example.com" };
        var model = new ViewerManagementViewModel { Rows = [ViewerRow(), pending] };

        var result = ViewerPage(await controller.ViewerManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("New Person", result.Rows[1].Name);
        Assert.False(result.Rows[1].LoginInstructionsSent);
    }

    [Fact]
    public async Task Viewer_SendInstructions_ForARowNotYetSaved_SendsNothingAndAsksToSave()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(loginInstructionsApiClient: login);
        var model = new ViewerManagementViewModel { Rows = [new ViewerRowViewModel { ViewerId = null, Name = "New Person", Email = "new@example.com" }] };

        var result = ViewerPage(await controller.ViewerManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.Empty(login.ViewerCalls);
        Assert.True(result.MessageIsError);
        Assert.Equal("Save your changes to this viewer before sending the login instructions.", result.Message);
        Assert.False(result.Rows[0].LoginInstructionsSent);
    }

    [Fact]
    public async Task Viewer_SendInstructions_AfterAnUnsavedEmailChange_SendsNothingAndAsksToSave()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(viewerApiClient: new FakeViewerApiClient { Viewers = [StoredViewer(sent: true)] }, loginInstructionsApiClient: login);
        var model = new ViewerManagementViewModel { Rows = [ViewerRow(email: "changed@example.com")] };

        var result = ViewerPage(await controller.ViewerManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.Empty(login.ViewerCalls);
        Assert.True(result.MessageIsError);
        Assert.False(result.Rows[0].LoginInstructionsSent);
    }

    [Fact]
    public async Task Viewer_SendInstructions_AlreadySentElsewhere_ShowsSentWithoutEmailingAgain()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(viewerApiClient: new FakeViewerApiClient { Viewers = [StoredViewer(sent: true)] }, loginInstructionsApiClient: login);
        var model = new ViewerManagementViewModel { Rows = [ViewerRow(sent: false)] };

        var result = ViewerPage(await controller.ViewerManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.Empty(login.ViewerCalls);
        Assert.True(result.Rows[0].LoginInstructionsSent);
    }

    [Fact]
    public async Task Viewer_SendInstructions_WhenTheEmailCannotBeSent_ShowsTheReasonAndKeepsTheLink()
    {
        var login = new FakeLoginInstructionsApiClient { Result = new LoginInstructionsResponse(false, "The login instructions email could not be sent. Please try again.") };
        var controller = SystemAdministrationControllerFactory.Create(viewerApiClient: new FakeViewerApiClient { Viewers = [StoredViewer(sent: false)] }, loginInstructionsApiClient: login);
        var model = new ViewerManagementViewModel { Rows = [ViewerRow()] };

        var result = ViewerPage(await controller.ViewerManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.True(result.MessageIsError);
        Assert.Equal("The login instructions email could not be sent. Please try again.", result.Message);
        Assert.False(result.Rows[0].LoginInstructionsSent);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Viewer_SendInstructions_WithARowIndexOutsideThePage_DoesNothing()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(loginInstructionsApiClient: login);
        var model = new ViewerManagementViewModel { Rows = [ViewerRow()] };

        var result = ViewerPage(await controller.ViewerManagement(model, PendingListCommands.SendInstructions, 5, CancellationToken.None));

        Assert.Empty(login.ViewerCalls);
        Assert.Null(result.Message);
    }

    [Theory]
    [InlineData("changed@example.com", false)]
    [InlineData("ALAN@example.com", true)]
    [InlineData("alan@example.com", true)]
    public async Task Viewer_EditedEmail_ResetsTheSentFlagOnlyWhenTheAddressChanges(string newEmail, bool expectedSent)
    {
        var controller = SystemAdministrationControllerFactory.Create();
        var model = new ViewerManagementViewModel
        {
            Rows = [ViewerRow(sent: true)],
            EditIndex = 0,
            Edit = new ViewerEditViewModel { Name = "Alan", Email = newEmail },
        };

        var result = ViewerPage(await controller.ViewerManagement(model, PendingListCommands.Update, 0, CancellationToken.None));

        Assert.Equal(expectedSent, result.Rows[0].LoginInstructionsSent);
    }

    [Fact]
    public async Task Viewer_EditedName_KeepsTheSentFlag()
    {
        var controller = SystemAdministrationControllerFactory.Create();
        var model = new ViewerManagementViewModel
        {
            Rows = [ViewerRow(sent: true)],
            EditIndex = 0,
            Edit = new ViewerEditViewModel { Name = "Alan Turing", Email = "alan@example.com" },
        };

        var result = ViewerPage(await controller.ViewerManagement(model, PendingListCommands.Update, 0, CancellationToken.None));

        Assert.True(result.Rows[0].LoginInstructionsSent);
    }

    [Fact]
    public async Task Viewer_Apply_TakesTheSentFlagFromTheDatabaseAfterTheSave()
    {
        // The database cleared the flag when the changed address was saved; the fake keeps returning that stored row.
        var api = new FakeViewerApiClient { Viewers = [StoredViewer(sent: false, email: "old@example.com")] };
        var controller = SystemAdministrationControllerFactory.Create(viewerApiClient: api);
        var model = new ViewerManagementViewModel { Rows = [ViewerRow(email: "changed@example.com", sent: true)] };

        var result = ViewerPage(await controller.ViewerManagement(model, PendingListCommands.Apply, null, CancellationToken.None));

        Assert.Single(api.UpdateCalls);
        Assert.False(result.Rows[0].LoginInstructionsSent);
    }

    // ---- External Test Consultants ----

    [Fact]
    public async Task Consultant_Get_ShowsTheStoredSentFlagOfEachRow()
    {
        var controller = SystemAdministrationControllerFactory.Create(externalTestConsultantApiClient: new FakeExternalTestConsultantApiClient { TestConsultants = [StoredConsultant(sent: true)] });

        var model = ConsultantPage(await controller.ExternalTestConsultantManagement(CancellationToken.None));

        Assert.True(Assert.Single(model.Rows).LoginInstructionsSent);
    }

    [Fact]
    public async Task Consultant_SendInstructions_EmailsTheSavedConsultantThenShowsItAsSent()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(
            externalTestConsultantApiClient: new FakeExternalTestConsultantApiClient { TestConsultants = [StoredConsultant(sent: false)] },
            loginInstructionsApiClient: login);
        var model = new ExternalTestConsultantManagementViewModel { Rows = [ConsultantRow()] };

        var result = ConsultantPage(await controller.ExternalTestConsultantManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.Equal(IdA, Assert.Single(login.ExternalTestConsultantCalls));
        Assert.True(result.Rows[0].LoginInstructionsSent);
        Assert.Equal("Login instructions sent to Alan.", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task Consultant_SendInstructions_ForARowNotYetSaved_SendsNothingAndAsksToSave()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(loginInstructionsApiClient: login);
        var model = new ExternalTestConsultantManagementViewModel { Rows = [new ExternalTestConsultantRowViewModel { Name = "New", Email = "new@example.com" }] };

        var result = ConsultantPage(await controller.ExternalTestConsultantManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.Empty(login.ExternalTestConsultantCalls);
        Assert.True(result.MessageIsError);
        Assert.Equal("Save your changes to this test consultant before sending the login instructions.", result.Message);
    }

    [Fact]
    public async Task Consultant_SendInstructions_AfterAnUnsavedEmailChange_SendsNothingAndAsksToSave()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(
            externalTestConsultantApiClient: new FakeExternalTestConsultantApiClient { TestConsultants = [StoredConsultant(sent: false)] },
            loginInstructionsApiClient: login);
        var model = new ExternalTestConsultantManagementViewModel { Rows = [ConsultantRow(email: "changed@example.com")] };

        var result = ConsultantPage(await controller.ExternalTestConsultantManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.Empty(login.ExternalTestConsultantCalls);
        Assert.True(result.MessageIsError);
    }

    [Fact]
    public async Task Consultant_SendInstructions_AlreadySentElsewhere_ShowsSentWithoutEmailingAgain()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(
            externalTestConsultantApiClient: new FakeExternalTestConsultantApiClient { TestConsultants = [StoredConsultant(sent: true)] },
            loginInstructionsApiClient: login);
        var model = new ExternalTestConsultantManagementViewModel { Rows = [ConsultantRow(sent: false)] };

        var result = ConsultantPage(await controller.ExternalTestConsultantManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.Empty(login.ExternalTestConsultantCalls);
        Assert.True(result.Rows[0].LoginInstructionsSent);
    }

    [Fact]
    public async Task Consultant_SendInstructions_ForAnInactiveRow_SendsNothingAndExplainsWhy()
    {
        var login = new FakeLoginInstructionsApiClient();
        var controller = SystemAdministrationControllerFactory.Create(
            externalTestConsultantApiClient: new FakeExternalTestConsultantApiClient { TestConsultants = [StoredConsultant(sent: false)] },
            loginInstructionsApiClient: login);
        var inactiveRow = ConsultantRow();
        inactiveRow.IsInactive = true;
        var model = new ExternalTestConsultantManagementViewModel { Rows = [inactiveRow] };

        var result = ConsultantPage(await controller.ExternalTestConsultantManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.Empty(login.ExternalTestConsultantCalls);
        Assert.True(result.MessageIsError);
        Assert.Equal("Login instructions can only be sent to an active test consultant.", result.Message);
        Assert.False(result.Rows[0].LoginInstructionsSent);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Consultant_SendInstructions_WhenTheEmailCannotBeSent_ShowsTheReasonAndKeepsTheLink()
    {
        var login = new FakeLoginInstructionsApiClient { Result = new LoginInstructionsResponse(false, "There is no email address saved to send the login instructions to.") };
        var controller = SystemAdministrationControllerFactory.Create(
            externalTestConsultantApiClient: new FakeExternalTestConsultantApiClient { TestConsultants = [StoredConsultant(sent: false)] },
            loginInstructionsApiClient: login);
        var model = new ExternalTestConsultantManagementViewModel { Rows = [ConsultantRow()] };

        var result = ConsultantPage(await controller.ExternalTestConsultantManagement(model, PendingListCommands.SendInstructions, 0, CancellationToken.None));

        Assert.True(result.MessageIsError);
        Assert.Equal("There is no email address saved to send the login instructions to.", result.Message);
        Assert.False(result.Rows[0].LoginInstructionsSent);
    }

    [Theory]
    [InlineData("changed@example.com", false)]
    [InlineData("ALAN@example.com", true)]
    public async Task Consultant_EditedEmail_ResetsTheSentFlagOnlyWhenTheAddressChanges(string newEmail, bool expectedSent)
    {
        var controller = SystemAdministrationControllerFactory.Create();
        var model = new ExternalTestConsultantManagementViewModel
        {
            Rows = [ConsultantRow(sent: true)],
            EditIndex = 0,
            Edit = new ExternalTestConsultantEditViewModel { Name = "Alan", Department = "Science", Email = newEmail },
        };

        var result = ConsultantPage(await controller.ExternalTestConsultantManagement(model, PendingListCommands.Update, 0, CancellationToken.None));

        Assert.Equal(expectedSent, result.Rows[0].LoginInstructionsSent);
    }

    [Fact]
    public async Task Consultant_Apply_TakesTheSentFlagFromTheDatabaseAfterTheSave()
    {
        var api = new FakeExternalTestConsultantApiClient { TestConsultants = [StoredConsultant(sent: false, email: "old@example.com")] };
        var controller = SystemAdministrationControllerFactory.Create(externalTestConsultantApiClient: api);
        var model = new ExternalTestConsultantManagementViewModel { Rows = [ConsultantRow(email: "changed@example.com", sent: true)] };

        var result = ConsultantPage(await controller.ExternalTestConsultantManagement(model, PendingListCommands.Apply, null, CancellationToken.None));

        Assert.False(result.Rows[0].LoginInstructionsSent);
    }
}
