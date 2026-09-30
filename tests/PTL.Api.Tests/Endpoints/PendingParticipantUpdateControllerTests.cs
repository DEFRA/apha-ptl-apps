using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Participant;
using PTL.Contracts.Participant;
using PTL.Core.Participant;

namespace PTL.Api.Tests.Endpoints;

public class PendingParticipantUpdateControllerTests
{
    private static ParticipantController CreateController(FakeParticipantRepository repository, FakePendingParticipantUpdateRepository pendingRepository)
    {
        var controller = new ParticipantController(new ParticipantService(repository, pendingRepository, NullLogger<ParticipantService>.Instance), NullLogger<ParticipantController>.Instance);

        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    private static PTL.Core.Participant.Participant ValidActiveParticipant() => new()
    {
        CustomerId = Guid.NewGuid(),
        SsoId = Guid.NewGuid(),
        LabCode = "001",
        LabName = "Alpha Lab",
        LabTypeId = Guid.NewGuid(),
        ContactName = "Alice Example",
        Organisation = "Alpha Organisation",
        Address1 = "1 Sample Street",
        Address2 = "Sample District",
        CountryId = Guid.NewGuid(),
        Telephone = "01234 567890",
        Email = "alice@example.com",
        IsActive = true
    };

    private static PendingParticipantUpdate ValidPendingUpdate(Guid participantId, string contactName = "New Contact") => new()
    {
        PendingParticipantUpdateId = Guid.NewGuid(),
        ParticipantId = participantId,
        LabCode = "001",
        ContactName = contactName,
        Organisation = "New Organisation",
        Address1 = "New Address 1",
        Address2 = "New Address 2",
        CountryId = Guid.NewGuid(),
        Telephone = "09876 543210",
        Email = "new@example.com",
        IsSubmitted = true
    };

    private static PendingParticipantUpdateSaveRequest AmendedRequest(string contactName) => new(
        contactName, "New Organisation", "New Address 1", "New Address 2", string.Empty, string.Empty, string.Empty,
        Guid.NewGuid(), "09876 543210", string.Empty, "new@example.com", string.Empty);

    [Fact]
    public async Task GetPendingParticipantUpdates_ReturnsSummaries()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(ValidPendingUpdate(participant.ParticipantId));
        var controller = CreateController(repository, pendingRepository);

        var result = await controller.GetPendingParticipantUpdates(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summaries = Assert.IsType<List<PendingParticipantUpdateSummaryResponse>>(ok.Value);
        Assert.Single(summaries);
        Assert.Equal(participant.ParticipantId, summaries[0].ParticipantId);
    }

    [Fact]
    public async Task GetPendingParticipantUpdate_NoPendingUpdate_ReturnsNotFound()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var controller = CreateController(repository, new FakePendingParticipantUpdateRepository());

        var result = await controller.GetPendingParticipantUpdate(participant.ParticipantId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetPendingParticipantUpdate_PendingUpdateExists_ReturnsComparison()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(ValidPendingUpdate(participant.ParticipantId));
        var controller = CreateController(repository, pendingRepository);

        var result = await controller.GetPendingParticipantUpdate(participant.ParticipantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var comparison = Assert.IsType<PendingParticipantUpdateComparisonResponse>(ok.Value);
        Assert.Equal(participant.ParticipantId, comparison.Current.ParticipantId);
        Assert.Equal("New Contact", comparison.Pending.ContactName);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdate_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantRepository(), new FakePendingParticipantUpdateRepository());

        var result = await controller.ApprovePendingParticipantUpdate(Guid.NewGuid(), request: null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdate_PendingUpdateExists_AppliesChangesAndReturnsNoContent()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(ValidPendingUpdate(participant.ParticipantId));
        var controller = CreateController(repository, pendingRepository);

        var result = await controller.ApprovePendingParticipantUpdate(participant.ParticipantId, request: null, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var updated = await repository.GetByIdAsync(participant.ParticipantId);
        Assert.Equal("New Contact", updated!.ContactName);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdate_AmendedValuesSupplied_AppliesAmendedValues()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(ValidPendingUpdate(participant.ParticipantId));
        var controller = CreateController(repository, pendingRepository);

        var result = await controller.ApprovePendingParticipantUpdate(participant.ParticipantId, AmendedRequest("Amended Contact"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var updated = await repository.GetByIdAsync(participant.ParticipantId);
        Assert.Equal("Amended Contact", updated!.ContactName);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdate_AmendedValuesBreakBusinessRules_ReturnsValidationProblem()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(ValidPendingUpdate(participant.ParticipantId));
        var controller = CreateController(repository, pendingRepository);

        var result = await controller.ApprovePendingParticipantUpdate(participant.ParticipantId, AmendedRequest(string.Empty), CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result, exactMatch: false);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdate_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeParticipantRepository(), new FakePendingParticipantUpdateRepository());

        var result = await controller.DeclinePendingParticipantUpdate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdate_PendingUpdateExists_DoesNotChangeParticipantAndReturnsNoContent()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(ValidPendingUpdate(participant.ParticipantId));
        var controller = CreateController(repository, pendingRepository);

        var result = await controller.DeclinePendingParticipantUpdate(participant.ParticipantId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var unchanged = await repository.GetByIdAsync(participant.ParticipantId);
        Assert.Equal("Alice Example", unchanged!.ContactName);
    }
}
