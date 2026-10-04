using Microsoft.Extensions.Logging.Abstractions;
using PTL.Core.Participant;
using ParticipantEntity = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Participant;

public class PendingParticipantUpdateServiceTests
{
    private static ParticipantService CreateService(FakeParticipantRepository repository, FakePendingParticipantUpdateRepository? pendingRepository = null) =>
        new(repository, pendingRepository ?? new FakePendingParticipantUpdateRepository(), new FakeParticipantViewerRepository(), new FakeViewerRepository(), NullLogger<ParticipantService>.Instance);

    private static ParticipantEntity ValidActiveParticipant() => new()
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

    // Approve runs ParticipantValidator against the resulting participant (legacy
    // mParticipant.IsValid), so this fixture must satisfy every required rule.
    private static PendingParticipantUpdate SamplePendingUpdate(Guid participantId) => new()
    {
        PendingParticipantUpdateId = Guid.NewGuid(),
        ParticipantId = participantId,
        LabCode = "001",
        ContactName = "New Contact",
        Organisation = "New Organisation",
        Address1 = "New Address 1",
        Address2 = "New Address 2",
        CountryId = Guid.NewGuid(),
        Telephone = "09876 543210",
        Email = "new@example.com",
        Email2 = "new-secondary@example.com",
        IsSubmitted = true
    };

    [Fact]
    public async Task GetPendingParticipantUpdatesAsync_ReturnsOutstandingSummaries()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var service = CreateService(repository, pendingRepository);

        var result = await service.GetPendingParticipantUpdatesAsync();

        Assert.Single(result);
        Assert.Equal(participant.ParticipantId, result[0].ParticipantId);
    }

    [Fact]
    public async Task GetPendingParticipantUpdateAsync_NoPendingUpdate_ReturnsNull()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var service = CreateService(repository);

        var result = await service.GetPendingParticipantUpdateAsync(participant.ParticipantId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPendingParticipantUpdateAsync_PendingUpdateExists_ReturnsCurrentAndPending()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var service = CreateService(repository, pendingRepository);

        var result = await service.GetPendingParticipantUpdateAsync(participant.ParticipantId);

        Assert.NotNull(result);
        Assert.Equal(participant.ParticipantId, result!.Value.Current.ParticipantId);
        Assert.Equal("New Contact", result.Value.Pending.ContactName);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_AppliesPendingFieldsAndSoftDeletes()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var service = CreateService(repository, pendingRepository);

        var approved = await service.ApprovePendingParticipantUpdateAsync(participant.ParticipantId);

        Assert.True(approved);
        var updated = await repository.GetByIdAsync(participant.ParticipantId);
        Assert.Equal("New Contact", updated!.ContactName);
        Assert.Equal("new-secondary@example.com", updated.Email2);
        Assert.Null(await service.GetPendingParticipantUpdateAsync(participant.ParticipantId));
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_NoPendingUpdate_ReturnsFalse()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var service = CreateService(repository);

        var approved = await service.ApprovePendingParticipantUpdateAsync(participant.ParticipantId);

        Assert.False(approved);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_EditedFieldsSupplied_AppliesEditedValues()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var service = CreateService(repository, pendingRepository);
        var edited = SamplePendingUpdate(participant.ParticipantId);
        edited.ContactName = "Amended Contact";

        var approved = await service.ApprovePendingParticipantUpdateAsync(participant.ParticipantId, edited);

        Assert.True(approved);
        var updated = await repository.GetByIdAsync(participant.ParticipantId);
        Assert.Equal("Amended Contact", updated!.ContactName);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_ResultingParticipantInvalid_ThrowsAndLeavesParticipantUnchanged()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var service = CreateService(repository, pendingRepository);
        var edited = SamplePendingUpdate(participant.ParticipantId);
        edited.ContactName = string.Empty;

        await Assert.ThrowsAsync<ParticipantValidationException>(() => service.ApprovePendingParticipantUpdateAsync(participant.ParticipantId, edited));

        var unchanged = await repository.GetByIdAsync(participant.ParticipantId);
        Assert.Equal("Alice Example", unchanged!.ContactName);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdateAsync_DoesNotChangeLiveParticipant()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var service = CreateService(repository, pendingRepository);

        var declined = await service.DeclinePendingParticipantUpdateAsync(participant.ParticipantId);

        Assert.True(declined);
        var unchanged = await repository.GetByIdAsync(participant.ParticipantId);
        Assert.Equal("Alice Example", unchanged!.ContactName);
        Assert.Null(await service.GetPendingParticipantUpdateAsync(participant.ParticipantId));
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdateAsync_NoPendingUpdate_ReturnsFalse()
    {
        var service = CreateService(new FakeParticipantRepository());

        var declined = await service.DeclinePendingParticipantUpdateAsync(Guid.NewGuid());

        Assert.False(declined);
    }
}
