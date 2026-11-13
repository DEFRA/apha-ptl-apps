using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.LoginInstructions;
using PTL.Contracts.Participant;
using PTL.InternalWeb.Features.Participant;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Participant;

// The Send Instructions link of Manage Participants' Login Instructions column.
public class ParticipantLoginInstructionsTests
{
    private static ParticipantController CreateController(FakeLoginInstructionsApiClient loginInstructions, ParticipantResponse? participant = null) =>
        new(new FakeParticipantApiClient { ParticipantResponse = participant }, new FakeCustomerApiClient(), new FakeLookupApiClient(), new FakeLabelPrinterApiClient(), loginInstructions, NullLogger<ParticipantController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static ParticipantResponse Participant(Guid participantId, Guid customerId, string contactName) =>
        new(participantId, Guid.NewGuid(), customerId, "ALI-001", "Alice Lab", Guid.NewGuid(),
            contactName, "Lab Name", "1 Street", "District", string.Empty, string.Empty, string.Empty,
            Guid.NewGuid(), "01234 567890", "01234 567891", "alice@example.com", "alice2@example.com",
            "Notes", true, null, false, null);

    [Fact]
    public async Task SendLoginInstructions_Sent_ShowsASuccessMessageNamingTheContactAndReturnsToTheSamePageOfTheList()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var loginInstructions = new FakeLoginInstructionsApiClient();
        var controller = CreateController(loginInstructions, Participant(participantId, customerId, "Alice Smith"));

        var result = await controller.SendLoginInstructions(participantId, customerId, includeInactive: true, page: 3, pageSize: 50, CancellationToken.None);

        Assert.Equal(participantId, Assert.Single(loginInstructions.ParticipantCalls));
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ParticipantController.Index), redirect.ActionName);
        Assert.Equal(customerId, redirect.RouteValues!["customerId"]);
        Assert.Equal(true, redirect.RouteValues["includeInactive"]);
        Assert.Equal(3, redirect.RouteValues["page"]);
        Assert.Equal(50, redirect.RouteValues["pageSize"]);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Login instructions sent to Alice Smith.", notification.Message);
    }

    [Fact]
    public async Task SendLoginInstructions_SentButTheContactNameIsNotAvailable_ShowsTheMessageWithoutAName()
    {
        var controller = CreateController(new FakeLoginInstructionsApiClient());

        await controller.SendLoginInstructions(Guid.NewGuid(), Guid.NewGuid(), cancellationToken: CancellationToken.None);

        Assert.Equal("Login instructions sent.", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task SendLoginInstructions_Refused_ShowsTheReasonAsAnErrorAndStillReturnsToTheList()
    {
        var loginInstructions = new FakeLoginInstructionsApiClient { Result = new LoginInstructionsResponse(false, "There is no email address saved to send the login instructions to.") };
        var controller = CreateController(loginInstructions);

        var result = await controller.SendLoginInstructions(Guid.NewGuid(), Guid.NewGuid(), cancellationToken: CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("There is no email address saved to send the login instructions to.", notification.Message);
    }

    [Fact]
    public async Task SendLoginInstructions_RefusedWithoutAReason_ShowsAGenericError()
    {
        var loginInstructions = new FakeLoginInstructionsApiClient { Result = new LoginInstructionsResponse(false) };
        var controller = CreateController(loginInstructions);

        await controller.SendLoginInstructions(Guid.NewGuid(), Guid.NewGuid(), cancellationToken: CancellationToken.None);

        Assert.Equal("The login instructions email could not be sent. Please try again.", controller.TempData.GetNotification()!.Message);
    }
}
