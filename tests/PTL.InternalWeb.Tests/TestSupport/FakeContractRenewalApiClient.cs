using PTL.ApiClient;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IContractRenewalApiClient so ContractController tests don't need a real HTTP
// call to PTL.Api.
internal sealed class FakeContractRenewalApiClient : IContractRenewalApiClient
{
    public RenewableContractsResponse ContractsResponse { get; set; } = new(false, null, [], []);

    public RenewableContractItemsResponse ItemsResponse { get; set; } = new([]);

    public RenewContractResponse RenewResult { get; set; } = new(true, Guid.NewGuid(), null);

    public Task<RenewableContractsResponse> GetRenewableContractsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ContractsResponse);

    public Task<RenewableContractItemsResponse> GetRenewableItemsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ItemsResponse);

    public Task<RenewContractResponse> RenewContractsAsync(Guid customerId, RenewContractRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(RenewResult);
}
