using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Tests.Contract;
using PTL.Core.Participant;
using CoreContract = PTL.Core.Contract.Contract;

namespace PTL.Api.Tests.Participant;

public sealed class ParticipantSchemeServiceTests
{
    private static ParticipantSchemeRecord ValidRecord(Guid contractId) => new()
    {
        ParticipantSchemeId = Guid.NewGuid(),
        ContractId = contractId,
        ParticipantId = Guid.NewGuid(),
        SchemeId = Guid.NewGuid(),
        NumberOfSetsRequired = 1,
        DistributionMonthJan = true
    };

    private static (ParticipantSchemeService Service, FakeParticipantSchemeRepository Repository, FakeContractRepository Contracts) CreateService()
    {
        var contracts = new FakeContractRepository();
        var repository = new FakeParticipantSchemeRepository();
        var service = new ParticipantSchemeService(repository, contracts, NullLogger<ParticipantSchemeService>.Instance);
        return (service, repository, contracts);
    }

    private static CoreContract ActiveContract(Guid contractId, bool isReadOnly = false) => new()
    {
        ContractId = contractId,
        CustomerId = Guid.NewGuid(),
        YearId = DateTime.UtcNow.Year,
        IsReadOnly = isReadOnly
    };

    [Fact]
    public async Task CreateParticipantSchemeAsync_ValidRecord_PersistsAndReturnsRecord()
    {
        var (service, repository, contracts) = CreateService();
        var contractId = Guid.NewGuid();
        contracts.Seed(ActiveContract(contractId));

        var created = await service.CreateParticipantSchemeAsync(ValidRecord(contractId));

        Assert.NotEqual(Guid.Empty, created.ParticipantSchemeId);
        Assert.NotNull(await repository.GetByIdAsync(created.ParticipantSchemeId));
    }

    [Fact]
    public async Task CreateParticipantSchemeAsync_MissingScheme_ThrowsValidationException()
    {
        var (service, _, contracts) = CreateService();
        var contractId = Guid.NewGuid();
        contracts.Seed(ActiveContract(contractId));
        var record = ValidRecord(contractId);
        record.SchemeId = Guid.Empty;

        var ex = await Assert.ThrowsAsync<ParticipantSchemeValidationException>(() => service.CreateParticipantSchemeAsync(record));
        Assert.Contains(ex.Errors, e => e.Field == "SchemeId");
    }

    [Fact]
    public async Task CreateParticipantSchemeAsync_ReadOnlyContract_ThrowsValidationException()
    {
        var (service, _, contracts) = CreateService();
        var contractId = Guid.NewGuid();
        contracts.Seed(ActiveContract(contractId, isReadOnly: true));

        await Assert.ThrowsAsync<ParticipantSchemeValidationException>(() => service.CreateParticipantSchemeAsync(ValidRecord(contractId)));
    }

    [Fact]
    public async Task UpdateParticipantSchemeAsync_UnknownId_ReturnsNull()
    {
        var (service, _, _) = CreateService();

        var result = await service.UpdateParticipantSchemeAsync(Guid.NewGuid(), ValidRecord(Guid.NewGuid()));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateParticipantSchemeAsync_ValidChanges_PersistsAndPreservesParticipantAndScheme()
    {
        var (service, repository, contracts) = CreateService();
        var contractId = Guid.NewGuid();
        contracts.Seed(ActiveContract(contractId));
        var existing = ValidRecord(contractId);
        repository.Add(existing);

        var updatedFields = ValidRecord(contractId);
        updatedFields.NumberOfSetsRequired = 3;
        updatedFields.SchemeId = Guid.NewGuid(); // attempt to change scheme - must be ignored

        var updated = await service.UpdateParticipantSchemeAsync(existing.ParticipantSchemeId, updatedFields);

        Assert.NotNull(updated);
        Assert.Equal(3, updated!.NumberOfSetsRequired);
        Assert.Equal(existing.SchemeId, updated.SchemeId);
        Assert.Equal(existing.ParticipantId, updated.ParticipantId);
    }

    [Fact]
    public async Task DeleteParticipantSchemeAsync_ExistingRecord_MarksRemoved()
    {
        var (service, repository, contracts) = CreateService();
        var contractId = Guid.NewGuid();
        contracts.Seed(ActiveContract(contractId));
        var existing = ValidRecord(contractId);
        repository.Add(existing);

        var removed = await service.DeleteParticipantSchemeAsync(existing.ParticipantSchemeId);

        Assert.True(removed);
        var stored = await repository.GetByIdAsync(existing.ParticipantSchemeId);
        Assert.True(stored!.IsRemoved);
    }

    [Fact]
    public async Task DeleteParticipantSchemeAsync_UnknownId_ReturnsFalse()
    {
        var (service, _, _) = CreateService();

        var removed = await service.DeleteParticipantSchemeAsync(Guid.NewGuid());

        Assert.False(removed);
    }

    [Fact]
    public async Task CreateParticipantSchemeAsync_ParticipantAlreadyOnSchemeInSameContract_ThrowsLegacyValidationError()
    {
        var (service, _, contracts) = CreateService();
        var contractId = Guid.NewGuid();
        contracts.Seed(ActiveContract(contractId));
        var record = ValidRecord(contractId);
        contracts.ContractItems = ItemsWith(contractId, record.SchemeId, record.ParticipantId, existingParticipantSchemeId: Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<ParticipantSchemeValidationException>(() => service.CreateParticipantSchemeAsync(record));

        Assert.Contains(ex.Errors, e => e.Field == "ParticipantId"
            && e.Message == "This participant is already on this scheme, on this contract. You should edit the existing contract item.");
    }

    [Fact]
    public async Task UpdateParticipantSchemeAsync_SameItemAlreadyOnScheme_IsNotTreatedAsDuplicate()
    {
        var (service, repository, contracts) = CreateService();
        var contractId = Guid.NewGuid();
        contracts.Seed(ActiveContract(contractId));
        var created = await service.CreateParticipantSchemeAsync(ValidRecord(contractId));
        contracts.ContractItems = ItemsWith(contractId, created.SchemeId, created.ParticipantId, created.ParticipantSchemeId);

        created.NumberOfSetsRequired = 3;
        var updated = await service.UpdateParticipantSchemeAsync(created.ParticipantSchemeId, created);

        Assert.Equal(3, updated!.NumberOfSetsRequired);
        Assert.Equal(3, (await repository.GetByIdAsync(created.ParticipantSchemeId))!.NumberOfSetsRequired);
    }

    [Fact]
    public async Task CreateParticipantSchemeAsync_DifferentParticipantOnSameScheme_IsAllowed()
    {
        var (service, _, contracts) = CreateService();
        var contractId = Guid.NewGuid();
        contracts.Seed(ActiveContract(contractId));
        var record = ValidRecord(contractId);
        contracts.ContractItems = ItemsWith(contractId, record.SchemeId, Guid.NewGuid(), Guid.NewGuid());

        var created = await service.CreateParticipantSchemeAsync(record);

        Assert.NotEqual(Guid.Empty, created.ParticipantSchemeId);
    }

    private static PTL.Core.Contract.ContractItemsAggregate ItemsWith(
        Guid contractId, Guid schemeId, Guid participantId, Guid existingParticipantSchemeId) => new()
        {
            ContractId = contractId,
            Schemes =
            [
                new PTL.Core.Contract.ContractItemSchemeGroup
                {
                    SchemeId = schemeId,
                    Participants =
                    [
                        new PTL.Core.Contract.ContractItemLine
                        {
                            ParticipantSchemeId = existingParticipantSchemeId,
                            ParticipantId = participantId
                        }
                    ]
                }
            ]
        };
}
