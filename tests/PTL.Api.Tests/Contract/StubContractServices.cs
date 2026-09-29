using PTL.Core.Contract.Renew;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;

namespace PTL.Api.Tests.Contract;

// ContractController is the single Contract-domain controller, so constructing it needs every
// Contract service. These stubs cover the ones a given test file does not exercise.
internal sealed class StubSampleAddressService : ISampleAddressService
{
    public Task<IReadOnlyList<SampleAddressEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SampleAddressEntity>>([]);
}

internal sealed class StubContractRenewalService : IContractRenewalService
{
    public Task<ContractRenewalEntity?> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ContractRenewalEntity?>(null);
}

internal sealed class StubRenewContractsService : IRenewContractsService
{
    public Task<RenewableContractsResult> GetRenewableContractsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RenewableContractsResult(new RenewEligibility(true, null), [], []));

    public Task<IReadOnlyList<RenewableContractItemEntity>> GetRenewableItemsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RenewableContractItemEntity>>([]);

    public Task<RenewContractsResult> RenewContractsAsync(
        Guid customerId,
        IReadOnlyList<Guid> contractIds,
        IReadOnlyList<Guid> participantSchemeIds,
        string? newContractSignatory,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new RenewContractsResult(true, Guid.NewGuid(), null));
}
