using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Participant;
using PTL.InternalWeb.Notifications;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Participant;

public class PendingParticipantUpdateControllerTests
{
    private static PTL.InternalWeb.Features.Participant.ParticipantController CreateController(FakeParticipantApiClient apiClient) =>
        new(apiClient, new FakeCustomerApiClient(), new FakeLookupApiClient(), NullLogger<PTL.InternalWeb.Features.Participant.ParticipantController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static ParticipantResponse SampleParticipant(Guid participantId) => new(
        participantId, Guid.NewGuid(), Guid.NewGuid(), "001", "Alpha Lab", Guid.NewGuid(), "Alice Example",
        "Alpha Organisation", "1 Sample Street", "Sample District", string.Empty, string.Empty, string.Empty,
        Guid.NewGuid(), "01234 567890", string.Empty, "alice@example.com", string.Empty, string.Empty, true, null, false, null);

    private static PendingParticipantUpdateResponse SamplePendingUpdate(Guid participantId) => new(
        participantId, Guid.NewGuid(), "001", "New Contact", "New Organisation", "New Address 1", string.Empty,
        string.Empty, string.Empty, string.Empty, Guid.NewGuid(), "09876 543210", string.Empty, "new@example.com", string.Empty);

    [Fact]
    public async Task ReviewPendingParticipantUpdates_ReturnsViewWithUpdates()
    {
        var apiClient = new FakeParticipantApiClient
        {
            PendingParticipantUpdates = [new PendingParticipantUpdateSummaryResponse(Guid.NewGuid(), "001", "Alpha Lab")]
        };
        var controller = CreateController(apiClient);

        var result = await controller.ReviewPendingParticipantUpdates(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Participant.PendingParticipantUpdateListViewModel>(view.Model);
        Assert.Single(model.Updates);
    }

    [Fact]
    public async Task PendingParticipantUpdateDetails_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantApiClient { PendingParticipantUpdateComparison = null });

        var result = await controller.PendingParticipantUpdateDetails(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PendingParticipantUpdateDetails_PendingUpdateExists_ReturnsViewWithAlignedComparisonRows()
    {
        var participantId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient
        {
            PendingParticipantUpdateComparison = new PendingParticipantUpdateComparisonResponse(SampleParticipant(participantId), SamplePendingUpdate(participantId))
        };
        var controller = CreateController(apiClient);

        var result = await controller.PendingParticipantUpdateDetails(participantId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Participant.PendingParticipantUpdateDetailsViewModel>(view.Model);
        Assert.Equal(12, model.ParticipantDetails.Count);
        var contactName = model.ParticipantDetails[0];
        Assert.Equal("Contact Name", contactName.Label);
        Assert.Equal("Alice Example", contactName.CurrentValue);
        Assert.Equal("New Contact", contactName.PendingValue);
        Assert.True(contactName.HasChanged);
        Assert.Equal("Email (Secondary)", model.ParticipantDetails[11].Label);
    }

    [Fact]
    public async Task EditPendingParticipantUpdate_Get_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantApiClient { PendingParticipantUpdateComparison = null });

        var result = await controller.EditPendingParticipantUpdate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditPendingParticipantUpdate_Get_PendingUpdateExists_ReturnsFormPopulatedWithPendingValues()
    {
        var participantId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient
        {
            PendingParticipantUpdateComparison = new PendingParticipantUpdateComparisonResponse(SampleParticipant(participantId), SamplePendingUpdate(participantId))
        };
        var controller = CreateController(apiClient);

        var result = await controller.EditPendingParticipantUpdate(participantId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Participant.PendingParticipantUpdateFormViewModel>(view.Model);
        Assert.Equal("New Contact", model.ContactName);
        Assert.Equal(participantId, model.ParticipantId);
    }

    [Fact]
    public async Task EditPendingParticipantUpdate_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController(new FakeParticipantApiClient());
        controller.ModelState.AddModelError("ContactName", "Enter a contact name");
        var model = new PTL.InternalWeb.Features.Participant.PendingParticipantUpdateFormViewModel();

        var result = await controller.EditPendingParticipantUpdate(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task EditPendingParticipantUpdate_Post_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantApiClient
        {
            ApprovePendingParticipantUpdateResult = new PendingParticipantUpdateDecisionResult(false, true, new Dictionary<string, string[]>())
        });

        var result = await controller.EditPendingParticipantUpdate(Guid.NewGuid(), new PTL.InternalWeb.Features.Participant.PendingParticipantUpdateFormViewModel(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditPendingParticipantUpdate_Post_ValidationFailure_ReturnsViewWithFieldErrors()
    {
        var controller = CreateController(new FakeParticipantApiClient
        {
            ApprovePendingParticipantUpdateResult = new PendingParticipantUpdateDecisionResult(false, false, new Dictionary<string, string[]> { ["ContactName"] = ["Contact name is required."] })
        });
        var model = new PTL.InternalWeb.Features.Participant.PendingParticipantUpdateFormViewModel();

        var result = await controller.EditPendingParticipantUpdate(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task EditPendingParticipantUpdate_Post_Success_SendsAmendedValuesAndRedirectsWithNotification()
    {
        var apiClient = new FakeParticipantApiClient();
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Participant.PendingParticipantUpdateFormViewModel { ContactName = "Amended Contact" };

        var result = await controller.EditPendingParticipantUpdate(Guid.NewGuid(), model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Participant.ParticipantController.ReviewPendingParticipantUpdates), redirect.ActionName);
        Assert.Equal("Amended Contact", apiClient.LastApproveRequest!.ContactName);
        Assert.Equal("Participant update approved successfully.", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdate_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantApiClient
        {
            ApprovePendingParticipantUpdateResult = new PendingParticipantUpdateDecisionResult(false, true, new Dictionary<string, string[]>())
        });

        var result = await controller.ApprovePendingParticipantUpdate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdate_ValidationFailure_RedirectsToEditPage()
    {
        var controller = CreateController(new FakeParticipantApiClient
        {
            ApprovePendingParticipantUpdateResult = new PendingParticipantUpdateDecisionResult(false, false, new Dictionary<string, string[]> { ["ContactName"] = ["Contact name is required."] })
        });

        var result = await controller.ApprovePendingParticipantUpdate(Guid.NewGuid(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Participant.ParticipantController.EditPendingParticipantUpdate), redirect.ActionName);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdate_Success_RedirectsToReviewListWithSuccessNotification()
    {
        var controller = CreateController(new FakeParticipantApiClient());

        var result = await controller.ApprovePendingParticipantUpdate(Guid.NewGuid(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Participant.ParticipantController.ReviewPendingParticipantUpdates), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Participant update approved successfully.", notification.Message);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdate_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantApiClient { DeclinePendingParticipantUpdateResult = false });

        var result = await controller.DeclinePendingParticipantUpdate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdate_Success_RedirectsToReviewListWithSuccessNotification()
    {
        var controller = CreateController(new FakeParticipantApiClient { DeclinePendingParticipantUpdateResult = true });

        var result = await controller.DeclinePendingParticipantUpdate(Guid.NewGuid(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Participant.ParticipantController.ReviewPendingParticipantUpdates), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Participant update declined successfully.", notification.Message);
    }
}
