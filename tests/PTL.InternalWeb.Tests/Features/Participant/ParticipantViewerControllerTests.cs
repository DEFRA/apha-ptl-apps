using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Participant;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Participant;

public class ParticipantViewerControllerTests
{
    private static PTL.InternalWeb.Features.Participant.ParticipantController CreateController(FakeParticipantApiClient apiClient) =>
        new(apiClient, new FakeCustomerApiClient(), new FakeLookupApiClient(), NullLogger<PTL.InternalWeb.Features.Participant.ParticipantController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static ParticipantViewerAssignmentResponse SampleAssignment(Guid customerId, bool isActive = true) => new(
        customerId,
        "LAB-01",
        "Sample Laboratory",
        isActive,
        [new ViewerResponse(Guid.NewGuid(), "Viewer One", "one@example.com")],
        [new ViewerResponse(Guid.NewGuid(), "Viewer Two", string.Empty)]);

    [Fact]
    public async Task Viewers_Get_UnknownParticipant_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantApiClient { ParticipantViewerAssignmentResponse = null });

        var result = await controller.Viewers(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Viewers_Get_ExistingParticipant_ReturnsViewWithModel()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeParticipantApiClient { ParticipantViewerAssignmentResponse = SampleAssignment(customerId) });

        var result = await controller.Viewers(participantId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Participant.ParticipantViewerFormViewModel>(view.Model);
        Assert.Equal(participantId, model.ParticipantId);
        Assert.Equal(customerId, model.CustomerId);
        Assert.Single(model.AvailableViewers);
        Assert.Single(model.AssignedViewers);
    }

    [Fact]
    public async Task Viewers_Get_InactiveParticipant_PopulatesIsActiveFalse()
    {
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeParticipantApiClient { ParticipantViewerAssignmentResponse = SampleAssignment(customerId, isActive: false) });

        var result = await controller.Viewers(Guid.NewGuid(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Participant.ParticipantViewerFormViewModel>(view.Model);
        Assert.False(model.IsActive);
    }

    [Fact]
    public async Task Viewers_Post_UnknownParticipant_ReturnsNotFound()
    {
        var apiClient = new FakeParticipantApiClient { UpdateParticipantViewersResult = false };
        var controller = CreateController(apiClient);

        var result = await controller.Viewers(Guid.NewGuid(), Guid.NewGuid(), [], CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Viewers_Post_Success_RedirectsToParticipantIndexWithNotification()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient { UpdateParticipantViewersResult = true };
        var controller = CreateController(apiClient);

        var result = await controller.Viewers(participantId, customerId, [viewerId], CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Participant.ParticipantController.Index), redirect.ActionName);
        Assert.Equal(customerId, redirect.RouteValues!["customerId"]);
        Assert.Equal(participantId, apiClient.LastUpdateParticipantViewersParticipantId);
        Assert.Equal([viewerId], apiClient.LastUpdateParticipantViewersViewerIds);
    }

    [Fact]
    public async Task Viewers_Post_EmptyViewerList_StillUpdatesSuccessfully()
    {
        var participantId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient { UpdateParticipantViewersResult = true };
        var controller = CreateController(apiClient);

        var result = await controller.Viewers(participantId, Guid.NewGuid(), [], CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(apiClient.LastUpdateParticipantViewersViewerIds!);
    }
}
