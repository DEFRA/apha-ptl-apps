using PTL.ApiClient;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IContractApiClient so ContractController tests don't need a real HTTP call
// to PTL.Api. Configure each scripted response via the constructor/settable properties.
internal sealed class FakeContractApiClient : IContractApiClient
{
    public ContractSearchResponse SearchResponse { get; set; } = new([], 0, 1, 20);
    public ContractResponse? ContractResponse { get; set; }
    public ContractSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());

    public Task<ContractResponse?> GetContractAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ContractResponse);

    public Task<ContractSearchResponse> GetContractsForCustomerAsync(Guid customerId, ContractSearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SearchResponse);

    public Task<ContractSearchResponse> GetContractsForCustomerByYearAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SearchResponse);

    public Task<ContractSaveResult> CreateContractAsync(Guid customerId, CreateContractRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);

    public Task<ContractSaveResult> UpdateContractAsync(Guid contractId, UpdateContractRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);
}
