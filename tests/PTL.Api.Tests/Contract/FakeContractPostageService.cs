using PTL.Core.Contract;

namespace PTL.Api.Tests.Contract;

// Records the postage hooks the services call; Counts, when set, is applied to the items read.
internal sealed class FakeContractPostageService : IContractPostageService
{
    public List<Guid> RecalculatedContractIds { get; } = [];

    public ContractPostageCounts? Counts { get; set; }

    public Task ApplyOnlineOrderCountsAsync(ContractItemsAggregate items, CancellationToken cancellationToken = default)
    {
        if (Counts is not null)
        {
            items.NumberCourier = Counts.Courier;
            items.NumberPostage = Counts.Postage;
            items.NumberSpecialDelivery = Counts.SpecialDelivery;
        }

        return Task.CompletedTask;
    }

    public Task RecalculateAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        RecalculatedContractIds.Add(contractId);
        return Task.CompletedTask;
    }
}
