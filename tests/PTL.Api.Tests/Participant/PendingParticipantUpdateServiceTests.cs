using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Tests.Notifications;
using PTL.Core.Participant;
using ParticipantEntity = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Participant;

public class PendingParticipantUpdateServiceTests
{
    private static ParticipantService CreateService(FakeParticipantRepository repository, FakePendingParticipantUpdateRepository? pendingRepository = null, FakeParticipantUpdateNotifier? notifier = null) =>
        new(repository, pendingRepository ?? new FakePendingParticipantUpdateRepository(), notifier ?? new FakeParticipantUpdateNotifier(), new FakeParticipantViewerRepository(), new FakeViewerRepository(), NullLogger<ParticipantService>.Instance);

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

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_QueuesAnAcceptedEmailForEachNewAddressAndSendsThem()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        var pending = SamplePendingUpdate(participant.ParticipantId);
        pendingRepository.Seed(pending);
        var notifier = new FakeParticipantUpdateNotifier();
        var service = CreateService(repository, pendingRepository, notifier);

        await service.ApprovePendingParticipantUpdateAsync(participant.ParticipantId, comments: "Thanks for the update");

        Assert.Equal(["new@example.com", "new-secondary@example.com"], pendingRepository.QueuedNotifications.Select(n => n.Email));
        Assert.All(pendingRepository.QueuedNotifications, n =>
        {
            Assert.Equal("001", n.LabCode);
            Assert.Equal("001: Alpha Lab", n.ParticipantName);
            Assert.Equal(pending.PendingParticipantUpdateId, n.PendingParticipantUpdateId);
            Assert.Equal("Thanks for the update", n.Notes);
            Assert.True(n.IsApproved);
        });
        Assert.Equal([pending.PendingParticipantUpdateId], notifier.SentForUpdates);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_NoSecondaryEmail_QueuesOneEmail()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var service = CreateService(repository, pendingRepository);
        var edited = SamplePendingUpdate(participant.ParticipantId);
        edited.Email2 = string.Empty;

        await service.ApprovePendingParticipantUpdateAsync(participant.ParticipantId, edited);

        Assert.Equal("new@example.com", Assert.Single(pendingRepository.QueuedNotifications).Email);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdateAsync_QueuesDeclinedEmailsForTheCurrentAddressesAndSendsThem()
    {
        var repository = new FakeParticipantRepository();
        var withSecondary = ValidActiveParticipant();
        withSecondary.Email2 = "alice-secondary@example.com";
        var participant = await repository.CreateAsync(withSecondary);
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        var pending = SamplePendingUpdate(participant.ParticipantId);
        pendingRepository.Seed(pending);
        var notifier = new FakeParticipantUpdateNotifier();
        var service = CreateService(repository, pendingRepository, notifier);

        await service.DeclinePendingParticipantUpdateAsync(participant.ParticipantId, "Not eligible");

        Assert.Equal(["alice@example.com", "alice-secondary@example.com"], pendingRepository.QueuedNotifications.Select(n => n.Email));
        Assert.All(pendingRepository.QueuedNotifications, n =>
        {
            Assert.False(n.IsApproved);
            Assert.Equal("Not eligible", n.Notes);
        });
        Assert.Equal([pending.PendingParticipantUpdateId], notifier.SentForUpdates);
    }

    [Fact]
    public async Task ApproveAndDecline_NothingDecided_QueueAndSendNothing()
    {
        var repository = new FakeParticipantRepository();
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        var notifier = new FakeParticipantUpdateNotifier();
        var service = CreateService(repository, pendingRepository, notifier);

        Assert.False(await service.ApprovePendingParticipantUpdateAsync(Guid.NewGuid()));
        Assert.False(await service.DeclinePendingParticipantUpdateAsync(Guid.NewGuid()));

        Assert.Empty(pendingRepository.QueuedNotifications);
        Assert.Empty(notifier.SentForUpdates);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdateAsync_NoEmailAddresses_QueuesAndSendsNothing()
    {
        var repository = new FakeParticipantRepository();
        var noEmail = ValidActiveParticipant();
        noEmail.Email = string.Empty;
        var participant = await repository.CreateAsync(noEmail);
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var notifier = new FakeParticipantUpdateNotifier();
        var service = CreateService(repository, pendingRepository, notifier);

        Assert.True(await service.DeclinePendingParticipantUpdateAsync(participant.ParticipantId));

        Assert.Empty(pendingRepository.QueuedNotifications);
        Assert.Empty(notifier.SentForUpdates);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdateAsync_LongValues_AreCutToTheQueueColumnLengths()
    {
        var repository = new FakeParticipantRepository();
        var longParticipant = ValidActiveParticipant();
        longParticipant.LabCode = "LABCODE123";
        longParticipant.LabName = new string('L', 80);
        longParticipant.Email = new string('e', 120);
        var participant = await repository.CreateAsync(longParticipant);
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var service = CreateService(repository, pendingRepository);

        await service.DeclinePendingParticipantUpdateAsync(participant.ParticipantId);

        var queued = Assert.Single(pendingRepository.QueuedNotifications);
        Assert.Equal(8, queued.LabCode.Length);
        Assert.Equal(50, queued.ParticipantName.Length);
        Assert.Equal(100, queued.Email.Length);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_CommentsTooLong_ThrowsAndChangesNothing()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var notifier = new FakeParticipantUpdateNotifier();
        var service = CreateService(repository, pendingRepository, notifier);

        var ex = await Assert.ThrowsAsync<ParticipantValidationException>(() => service.ApprovePendingParticipantUpdateAsync(participant.ParticipantId, comments: new string('x', 2001)));

        Assert.Equal("Comments", Assert.Single(ex.Errors).Field);
        Assert.Equal("Alice Example", (await repository.GetByIdAsync(participant.ParticipantId))!.ContactName);
        Assert.NotNull(await pendingRepository.GetByParticipantIdAsync(participant.ParticipantId));
        Assert.Empty(pendingRepository.QueuedNotifications);
        Assert.Empty(notifier.SentForUpdates);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdateAsync_CommentsTooLong_ThrowsAndKeepsTheUpdatePending()
    {
        var repository = new FakeParticipantRepository();
        var participant = await repository.CreateAsync(ValidActiveParticipant());
        var pendingRepository = new FakePendingParticipantUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(participant.ParticipantId));
        var service = CreateService(repository, pendingRepository);

        await Assert.ThrowsAsync<ParticipantValidationException>(() => service.DeclinePendingParticipantUpdateAsync(participant.ParticipantId, new string('x', 2001)));

        Assert.NotNull(await pendingRepository.GetByParticipantIdAsync(participant.ParticipantId));
    }
}
