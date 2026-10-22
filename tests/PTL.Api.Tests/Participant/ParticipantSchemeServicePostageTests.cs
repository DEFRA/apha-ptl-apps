using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Tests.Contract;
using PTL.Core.Participant;
using CoreContract = PTL.Core.Contract.Contract;

namespace PTL.Api.Tests.Participant;

// The Contract Item save hooks the legacy ParticipantScheme page ran after ps.Save().
public sealed class ParticipantSchemeServicePostageTests
{
    private static (ParticipantSchemeService Service, FakeContractPostageService Postage, FakeParticipantSchemeRepository Repository, Guid ContractId) Create()
    {
        var contractId = Guid.NewGuid();
        var contracts = new FakeContractRepository();
        contracts.Seed(new CoreContract { ContractId = contractId, CustomerId = Guid.NewGuid(), YearId = DateTime.UtcNow.Year });
        var postage = new FakeContractPostageService();
        var repository = new FakeParticipantSchemeRepository();
        var service = new ParticipantSchemeService(repository, contracts, new PTL.Api.Tests.Customer.FakeCustomerRepository(), postage, NullLogger<ParticipantSchemeService>.Instance);
        return (service, postage, repository, contractId);
    }

    private static ParticipantSchemeRecord Record(Guid contractId) => new()
    {
        ContractId = contractId,
        ParticipantId = Guid.NewGuid(),
        SchemeId = Guid.NewGuid(),
        NumberOfSetsRequired = 1,
        DistributionMonthJan = true
    };

    [Fact]
    public async Task Create_RecalculatesTheContractsPostage()
    {
        var (service, postage, _, contractId) = Create();

        await service.CreateParticipantSchemeAsync(Record(contractId));

        Assert.Equal([contractId], postage.RecalculatedContractIds);
    }

    [Fact]
    public async Task Update_RecalculatesTheContractsPostage()
    {
        var (service, postage, _, contractId) = Create();
        var created = await service.CreateParticipantSchemeAsync(Record(contractId));
        postage.RecalculatedContractIds.Clear();

        await service.UpdateParticipantSchemeAsync(created.ParticipantSchemeId, Record(contractId));

        Assert.Equal([contractId], postage.RecalculatedContractIds);
    }

    [Fact]
    public async Task FailedValidation_DoesNotRecalculate()
    {
        var (service, postage, _, contractId) = Create();
        var record = Record(contractId);
        record.SchemeId = Guid.Empty;

        await Assert.ThrowsAsync<ParticipantSchemeValidationException>(() => service.CreateParticipantSchemeAsync(record));

        Assert.Empty(postage.RecalculatedContractIds);
    }
}
