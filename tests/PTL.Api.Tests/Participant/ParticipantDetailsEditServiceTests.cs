using PTL.Core.Participant;

namespace PTL.Api.Tests.Participant;

public class ParticipantDetailsEditServiceTests
{
    private static (ParticipantDetailsEditService Service, FakeParticipantRepository Participants, FakePendingParticipantUpdateRepository PendingUpdates) CreateService()
    {
        var participants = new FakeParticipantRepository();
        var pendingUpdates = new FakePendingParticipantUpdateRepository();
        var service = new ParticipantDetailsEditService(participants, pendingUpdates);
        return (service, participants, pendingUpdates);
    }

    [Fact]
    public async Task GetAsync_ParticipantNotFound_ReturnsNull()
    {
        var (service, _, _) = CreateService();

        var result = await service.GetAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_NoOutstandingPendingUpdate_ReturnsLiveParticipantValues()
    {
        var (service, participants, _) = CreateService();
        var participant = await participants.CreateAsync(new PTL.Core.Participant.Participant { LabCode = "001", ContactName = "Live Contact" });

        var result = await service.GetAsync(participant.ParticipantId);

        Assert.NotNull(result);
        Assert.False(result!.HasOutstandingUpdate);
        Assert.Equal("001", result.LabCode);
        Assert.Equal("Live Contact", result.Values.ContactName);
    }

    [Fact]
    public async Task GetAsync_OutstandingPendingUpdateExists_ReturnsPendingValues()
    {
        var (service, participants, pendingUpdates) = CreateService();
        var participant = await participants.CreateAsync(new PTL.Core.Participant.Participant { LabCode = "001", ContactName = "Live Contact" });
        await pendingUpdates.UpsertAsync(new PendingParticipantUpdate { ParticipantId = participant.ParticipantId, ContactName = "Pending Contact", IsSubmitted = true });

        var result = await service.GetAsync(participant.ParticipantId);

        Assert.NotNull(result);
        Assert.True(result!.HasOutstandingUpdate);
        Assert.Equal("Pending Contact", result.Values.ContactName);
    }

    [Fact]
    public async Task SubmitAsync_InvalidData_ThrowsParticipantValidationExceptionAndDoesNotPersist()
    {
        var (service, participants, pendingUpdates) = CreateService();
        var participant = await participants.CreateAsync(new PTL.Core.Participant.Participant { LabCode = "001" });

        await Assert.ThrowsAsync<ParticipantValidationException>(() =>
            service.SubmitAsync(participant.ParticipantId, new PendingParticipantUpdate()));

        Assert.Null(await pendingUpdates.GetByParticipantIdAsync(participant.ParticipantId));
    }

    [Fact]
    public async Task SubmitAsync_OutstandingPendingUpdateAlreadyExists_ThrowsAndDoesNotOverwrite()
    {
        var (service, participants, pendingUpdates) = CreateService();
        var participant = await participants.CreateAsync(new PTL.Core.Participant.Participant { LabCode = "001" });
        await pendingUpdates.UpsertAsync(new PendingParticipantUpdate { ParticipantId = participant.ParticipantId, ContactName = "Already Submitted", IsSubmitted = true });

        await Assert.ThrowsAsync<PendingParticipantUpdateAlreadySubmittedException>(() =>
            service.SubmitAsync(participant.ParticipantId, new PendingParticipantUpdate { ContactName = "New Attempt" }));

        var stillStored = await pendingUpdates.GetByParticipantIdAsync(participant.ParticipantId);
        Assert.Equal("Already Submitted", stillStored!.ContactName);
    }

    [Fact]
    public async Task SubmitAsync_ValidDataAndNoOutstandingUpdate_PersistsWithResolvedIdsAndSubmittedFlag()
    {
        var (service, participants, pendingUpdates) = CreateService();
        var participant = await participants.CreateAsync(new PTL.Core.Participant.Participant { LabCode = "001", CustomerId = Guid.NewGuid() });

        var proposed = new PendingParticipantUpdate
        {
            ContactName = "Jane Doe",
            Organisation = "Test Lab",
            Address1 = "1 Test Street",
            Address2 = "Testville",
            CountryId = Guid.NewGuid(),
            Telephone = "01234 567890",
            Email = "jane@example.com"
        };

        await service.SubmitAsync(participant.ParticipantId, proposed);

        var stored = await pendingUpdates.GetByParticipantIdAsync(participant.ParticipantId);
        Assert.NotNull(stored);
        Assert.Equal(participant.ParticipantId, stored!.ParticipantId);
        Assert.Equal(participant.CustomerId, stored.CustomerId);
        Assert.Equal("001", stored.LabCode);
        Assert.True(stored.IsSubmitted);
        Assert.False(stored.IsDeleted);
        Assert.Equal("Jane Doe", stored.ContactName);
    }

    [Fact]
    public async Task SubmitAsync_FaxNotOnEditForm_CarriesOverLiveParticipantValue()
    {
        var (service, participants, pendingUpdates) = CreateService();
        var participant = await participants.CreateAsync(new PTL.Core.Participant.Participant { LabCode = "001", Fax = "01234 111111" });

        var proposed = new PendingParticipantUpdate
        {
            ContactName = "Jane Doe",
            Organisation = "Test Lab",
            Address1 = "1 Test Street",
            Address2 = "Testville",
            CountryId = Guid.NewGuid(),
            Telephone = "01234 567890",
            Email = "jane@example.com"
        };

        await service.SubmitAsync(participant.ParticipantId, proposed);

        var stored = await pendingUpdates.GetByParticipantIdAsync(participant.ParticipantId);
        Assert.NotNull(stored);
        Assert.Equal("01234 111111", stored!.Fax);
    }
}
