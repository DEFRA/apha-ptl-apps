using Microsoft.Extensions.Logging.Abstractions;
using PTL.Core.Participant;
using PTL.Core.Viewer;
using ParticipantEntity = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Participant;

public class ParticipantServiceTests
{
    private static ParticipantService CreateService(
        FakeParticipantRepository repository,
        FakePendingParticipantUpdateRepository? pendingRepository = null,
        FakeParticipantViewerRepository? participantViewerRepository = null,
        FakeViewerRepository? viewerRepository = null) =>
        new(
            repository,
            pendingRepository ?? new FakePendingParticipantUpdateRepository(),
            participantViewerRepository ?? new FakeParticipantViewerRepository(),
            viewerRepository ?? new FakeViewerRepository(),
            NullLogger<ParticipantService>.Instance);

    private static ParticipantEntity ValidActiveParticipant(string labName = "Alpha Lab") => new()
    {
        CustomerId = Guid.NewGuid(),
        SsoId = Guid.NewGuid(),
        LabCode = "001",
        LabName = labName,
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

    [Fact]
    public async Task CreateParticipantAsync_ValidParticipant_PersistsAndAssignsId()
    {
        var service = CreateService(new FakeParticipantRepository());

        var created = await service.CreateParticipantAsync(ValidActiveParticipant());

        Assert.NotEqual(Guid.Empty, created.ParticipantId);
        Assert.True(created.IsActive);
        Assert.Equal("001", created.LabCode);
    }

    [Fact]
    public async Task CreateParticipantAsync_WithoutSsoId_GeneratesOne()
    {
        var service = CreateService(new FakeParticipantRepository());
        var participant = ValidActiveParticipant();
        participant.SsoId = Guid.Empty;

        var created = await service.CreateParticipantAsync(participant);

        Assert.NotEqual(Guid.Empty, created.SsoId);
    }

    [Fact]
    public async Task UpdateParticipantAsync_SetIsActiveFalse_StampsInactiveDate()
    {
        var repository = new FakeParticipantRepository();
        var service = CreateService(repository);
        var created = await service.CreateParticipantAsync(ValidActiveParticipant());
        var updatedFields = ValidActiveParticipant();
        updatedFields.IsActive = false;

        var deactivated = await service.UpdateParticipantAsync(created.ParticipantId, updatedFields);

        Assert.NotNull(deactivated);
        Assert.False(deactivated!.IsActive);
        Assert.NotNull(deactivated.InactiveDate);
    }

    [Fact]
    public async Task GetParticipantViewersAsync_UnknownParticipant_ReturnsNull()
    {
        var service = CreateService(new FakeParticipantRepository());

        var result = await service.GetParticipantViewersAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetParticipantViewersAsync_NoAssignedViewers_AllViewersAvailable()
    {
        var repository = new FakeParticipantRepository();
        var service = CreateService(repository, viewerRepository: new FakeViewerRepository());
        var created = await repository.CreateAsync(ValidActiveParticipant());
        var viewerRepository = new FakeViewerRepository();
        viewerRepository.Viewers.Add(new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "Viewer One", Email = "one@example.com" });
        var serviceWithViewer = CreateService(repository, viewerRepository: viewerRepository);

        var result = await serviceWithViewer.GetParticipantViewersAsync(created.ParticipantId);

        Assert.NotNull(result);
        Assert.Single(result!.AvailableViewers);
        Assert.Empty(result.AssignedViewers);
    }

    [Fact]
    public async Task GetParticipantViewersAsync_AssignedViewer_ExcludedFromAvailable()
    {
        var repository = new FakeParticipantRepository();
        var created = await repository.CreateAsync(ValidActiveParticipant());
        var viewerId = Guid.NewGuid();
        var viewerRepository = new FakeViewerRepository();
        viewerRepository.Viewers.Add(new ViewerEntity { ViewerId = viewerId, Name = "Viewer One", Email = "one@example.com" });
        var participantViewerRepository = new FakeParticipantViewerRepository();
        await participantViewerRepository.AddAsync(Guid.NewGuid(), viewerId, created.ParticipantId);
        var service = CreateService(repository, participantViewerRepository: participantViewerRepository, viewerRepository: viewerRepository);

        var result = await service.GetParticipantViewersAsync(created.ParticipantId);

        Assert.NotNull(result);
        Assert.Empty(result!.AvailableViewers);
        Assert.Single(result.AssignedViewers);
        Assert.Equal(viewerId, result.AssignedViewers[0].ViewerId);
    }

    [Fact]
    public async Task UpdateParticipantViewersAsync_UnknownParticipant_ReturnsFalse()
    {
        var service = CreateService(new FakeParticipantRepository());

        var result = await service.UpdateParticipantViewersAsync(Guid.NewGuid(), []);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateParticipantViewersAsync_AddsNewViewer()
    {
        var repository = new FakeParticipantRepository();
        var created = await repository.CreateAsync(ValidActiveParticipant());
        var participantViewerRepository = new FakeParticipantViewerRepository();
        var service = CreateService(repository, participantViewerRepository: participantViewerRepository);
        var viewerId = Guid.NewGuid();

        var result = await service.UpdateParticipantViewersAsync(created.ParticipantId, [viewerId]);

        Assert.True(result);
        Assert.Single(participantViewerRepository.Assignments);
        Assert.Equal(viewerId, participantViewerRepository.Assignments[0].ViewerId);
    }

    [Fact]
    public async Task UpdateParticipantViewersAsync_RemovesViewerNotInTargetList()
    {
        var repository = new FakeParticipantRepository();
        var created = await repository.CreateAsync(ValidActiveParticipant());
        var participantViewerRepository = new FakeParticipantViewerRepository();
        var viewerId = Guid.NewGuid();
        await participantViewerRepository.AddAsync(Guid.NewGuid(), viewerId, created.ParticipantId);
        var service = CreateService(repository, participantViewerRepository: participantViewerRepository);

        var result = await service.UpdateParticipantViewersAsync(created.ParticipantId, []);

        Assert.True(result);
        Assert.Empty(participantViewerRepository.Assignments);
    }

    [Fact]
    public async Task UpdateParticipantViewersAsync_SameTargetList_NoChanges()
    {
        var repository = new FakeParticipantRepository();
        var created = await repository.CreateAsync(ValidActiveParticipant());
        var participantViewerRepository = new FakeParticipantViewerRepository();
        var viewerId = Guid.NewGuid();
        await participantViewerRepository.AddAsync(Guid.NewGuid(), viewerId, created.ParticipantId);
        var service = CreateService(repository, participantViewerRepository: participantViewerRepository);

        var result = await service.UpdateParticipantViewersAsync(created.ParticipantId, [viewerId]);

        Assert.True(result);
        Assert.Single(participantViewerRepository.Assignments);
        Assert.Equal(viewerId, participantViewerRepository.Assignments[0].ViewerId);
    }
}
