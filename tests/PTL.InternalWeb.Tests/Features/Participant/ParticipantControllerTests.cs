using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Participant;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Participant;

public class ParticipantControllerTests
{
    private static PTL.InternalWeb.Features.Participant.ParticipantController CreateController(FakeParticipantApiClient apiClient, FakeCustomerApiClient? customerApiClient = null) =>
        new(apiClient, customerApiClient ?? new FakeCustomerApiClient(), new FakeLookupApiClient(), NullLogger<PTL.InternalWeb.Features.Participant.ParticipantController>.Instance);

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

    private static PTL.Contracts.Customer.CustomerResponse SampleCustomer(Guid customerId) => new(
        customerId, "QAL0001", string.Empty, "Sample Customer", string.Empty, Guid.NewGuid(), string.Empty,
        Guid.NewGuid(), string.Empty, string.Empty, "Bob Contact", "Sample Org", "1 Customer Street",
        string.Empty, string.Empty, string.Empty, string.Empty, Guid.NewGuid(), "01234 000000", string.Empty,
        string.Empty, "bob@example.com", Guid.NewGuid(), string.Empty, DateTime.UtcNow, string.Empty, false,
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
        Guid.NewGuid(), string.Empty, string.Empty, string.Empty, string.Empty, true, false, null, null);

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

    [Fact]
    public async Task Create_Get_CustomerFound_PopulatesCustomerContact()
    {
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeParticipantApiClient(), new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) });

        var result = await controller.Create(customerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Participant.ParticipantFormViewModel>(view.Model);
        Assert.Equal("Bob Contact", model.CustomerContactName);
    }

    [Fact]
    public async Task Create_Post_ThrowsException_ReturnsViewWithError()
    {
        var customerId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient { ExceptionToThrow = new InvalidOperationException("boom") };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel { CustomerId = customerId, LabCode = "LAB-01", LabName = "Sample Laboratory" };

        var result = await controller.Create(customerId, model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Edit_Get_ExistingParticipant_ReturnsViewWithFormViewModel()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeParticipantApiClient { ParticipantResponse = SampleParticipant(participantId, customerId) });

        var result = await controller.Edit(participantId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Participant.ParticipantFormViewModel>(view.Model);
        Assert.Equal(participantId, model.ParticipantId);
    }

    [Fact]
    public async Task Edit_Post_InvalidModelState_RestoresDisplayOnlyFieldsAndReturnsView()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient { ParticipantResponse = SampleParticipant(participantId, customerId) };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("LabName", "Enter a lab name.");
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel { ParticipantId = participantId, CustomerId = customerId };

        var result = await controller.Edit(participantId, model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.Equal(apiClient.ParticipantResponse.SsoId, model.SsoId);
    }

    [Fact]
    public async Task Edit_Post_InvalidModelState_UnknownParticipant_StillReturnsView()
    {
        var participantId = Guid.NewGuid();
        var controller = CreateController(new FakeParticipantApiClient { ParticipantResponse = null });
        controller.ModelState.AddModelError("LabName", "Enter a lab name.");
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel { ParticipantId = participantId };

        var result = await controller.Edit(participantId, model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Edit_Post_UpdateReturnsNull_ReturnsNotFound()
    {
        var participantId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient { UpdateReturnsNull = true };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel
        {
            ParticipantId = participantId,
            CustomerId = Guid.NewGuid(),
            LabCode = "LAB-01",
            LabName = "Sample Laboratory",
            ContactName = "Alice Example",
            Organisation = "Sample Laboratory Ltd",
            Address1 = "1 Sample Street",
            Email = "alice@example.com",
            IsActive = true
        };

        var result = await controller.Edit(participantId, model, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_ThrowsException_ReturnsViewWithError()
    {
        var participantId = Guid.NewGuid();
        var apiClient = new FakeParticipantApiClient { ExceptionToThrow = new InvalidOperationException("boom") };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Participant.ParticipantFormViewModel
        {
            ParticipantId = participantId,
            CustomerId = Guid.NewGuid(),
            LabCode = "LAB-01",
            LabName = "Sample Laboratory"
        };

        var result = await controller.Edit(participantId, model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }
}
