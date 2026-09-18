using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Participant;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Participant;

public class ParticipantControllerTests
{
    private static PTL.InternalWeb.Features.Participant.ParticipantController CreateController(FakeParticipantApiClient apiClient) =>
        new(apiClient, new FakeCustomerApiClient(), new FakeLookupApiClient(), NullLogger<PTL.InternalWeb.Features.Participant.ParticipantController>.Instance);

    private static ParticipantResponse SampleParticipant(Guid participantId, Guid customerId, bool isActive = true) => new(
        participantId,
        Guid.NewGuid(),
        customerId,
        "LAB-01",
        "Sample Laboratory",
        Guid.NewGuid(),
        "Alice Example",
        "Sample Laboratory Ltd",
        "1 Sample Street",
        "Sample District",
        string.Empty,
        string.Empty,
        string.Empty,
        Guid.NewGuid(),
        "01234 567890",
        "",
        "alice@example.com",
        string.Empty,
        "Sample comments",
        isActive,
        isActive ? null : DateTime.UtcNow,
        false,
        null);

    [Fact]
    public async Task Index_ReturnsViewWithSearchResults()
    {
        var customerId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient
        {
            SearchResponse = new ParticipantSearchResponse(
                [new ParticipantSummaryResponse(participantId, customerId, "LAB-01", "Sample Laboratory", "Alice Example", true)],
                1,
                1,
                20)
        };
        var controller = CreateController(apiClient);

        var result = await controller.Index(customerId, "Sample", false, 1, 20, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Participant.ParticipantListViewModel>(view.Model);
        Assert.Single(model.Participants);
        Assert.Equal(1, model.TotalCount);
    }

    [Fact]
    public async Task Details_UnknownParticipant_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantApiClient { ParticipantResponse = null });

        var result = await controller.Details(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ExistingParticipant_ReturnsViewWithModel()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeParticipantApiClient { ParticipantResponse = SampleParticipant(participantId, customerId) });

        var result = await controller.Details(participantId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(participantId, Assert.IsType<ParticipantResponse>(view.Model).ParticipantId);
    }

    [Fact]
    public async Task Create_Get_ReturnsEmptyFormViewModel()
    {
        var controller = CreateController(new FakeParticipantApiClient());

        var result = await controller.Create(Guid.NewGuid(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<PTL.InternalWeb.Features.Participant.ParticipantFormViewModel>(view.Model);
    }

    [Fact]
    public async Task Create_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeParticipantApiClient());
        controller.ModelState.AddModelError("LabName", "Enter a lab name.");
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel { CustomerId = customerId };

        var result = await controller.Create(customerId, model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Create_Post_Success_RedirectsToDetails()
    {
        var customerId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient
        {
            CreatedOrUpdatedResponse = SampleParticipant(participantId, customerId)
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel
        {
            CustomerId = customerId,
            LabCode = "LAB-01",
            LabName = "Sample Laboratory",
            ContactName = "Alice Example",
            Organisation = "Sample Laboratory Ltd",
            Address1 = "1 Sample Street",
            Email = "alice@example.com",
            IsActive = true
        };

        var result = await controller.Create(customerId, model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Participant.ParticipantController.Details), redirect.ActionName);
        Assert.Equal(participantId, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Edit_Get_UnknownParticipant_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantApiClient { ParticipantResponse = null });

        var result = await controller.Edit(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_Success_RedirectsToDetails()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient
        {
            CreatedOrUpdatedResponse = SampleParticipant(participantId, customerId)
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel
        {
            ParticipantId = participantId,
            CustomerId = customerId,
            LabCode = "LAB-01",
            LabName = "Sample Laboratory",
            ContactName = "Alice Example",
            Organisation = "Sample Laboratory Ltd",
            Address1 = "1 Sample Street",
            Email = "alice@example.com",
            IsActive = true
        };

        var result = await controller.Edit(participantId, model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PTL.InternalWeb.Features.Participant.ParticipantController.Details), redirect.ActionName);
    }
}
