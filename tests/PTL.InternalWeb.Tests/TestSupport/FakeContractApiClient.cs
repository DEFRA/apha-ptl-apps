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

    public ContractSearchRequest? LastSearchRequest { get; private set; }

    public Task<ContractSearchResponse> GetContractsForCustomerAsync(Guid customerId, ContractSearchRequest request, CancellationToken cancellationToken = default)
    {
        LastSearchRequest = request;
        return Task.FromResult(SearchResponse);
    }

    public Task<ContractSearchResponse> GetContractsForCustomerByYearAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SearchResponse);

    public Task<ContractSaveResult> CreateContractAsync(Guid customerId, ContractRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);

    public Task<ContractSaveResult> UpdateContractAsync(Guid contractId, ContractRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);

    public ContractItemsResponse? ItemsResponse { get; set; }
    public ContractItemRemovalResult RemovalResult { get; set; } = new(true, false, null);

    // What the page asked for: the items it had marked for removal, and the audited removals it saved.
    public IReadOnlyCollection<Guid>? LastPendingRemovalIds { get; private set; }
    public List<(Guid ParticipantSchemeId, string? AuditWhy)> Removals { get; } = [];

    public Task<ContractItemsResponse?> GetContractItemsAsync(Guid contractId, IReadOnlyCollection<Guid>? pendingRemovalIds = null, CancellationToken cancellationToken = default)
    {
        LastPendingRemovalIds = pendingRemovalIds;
        return Task.FromResult(ItemsResponse);
    }

    public Task<ContractItemRemovalResult> RemoveContractItemAsync(Guid contractId, Guid participantSchemeId, string? auditWhy = null, CancellationToken cancellationToken = default)
    {
        Removals.Add((participantSchemeId, auditWhy));
        return Task.FromResult(RemovalResult);
    }

    public PendingOrderListResponse PendingOrders { get; set; } = new([], []);
    public PendingOrderDetailsResponse? PendingOrder { get; set; }
    public bool UpdatePendingOrderSchemeResult { get; set; } = true;
    public PendingOrderDecisionResult ApprovePendingOrderResult { get; set; } = new(true, false, new Dictionary<string, string[]>());
    public PendingOrderDecisionResult DeclinePendingOrderResult { get; set; } = new(true, false, new Dictionary<string, string[]>());
    public PendingOrderDeclineRequest? LastDeclineRequest { get; private set; }
    public PendingOrderSchemeUpdateRequest? LastSchemeUpdate { get; private set; }
    public PendingOrderApproveRequest? LastApproveRequest { get; private set; }

    public Task<PendingOrderListResponse> GetPendingOrdersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(PendingOrders);

    public Task<PendingOrderDetailsResponse?> GetPendingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PendingOrder);

    public Task<bool> UpdatePendingOrderSchemeAsync(Guid pendingContractId, Guid pendingParticipantSchemeId, PendingOrderSchemeUpdateRequest request, CancellationToken cancellationToken = default)
    {
        LastSchemeUpdate = request;
        return Task.FromResult(UpdatePendingOrderSchemeResult);
    }

    public Task<PendingOrderDecisionResult> ApprovePendingOrderAsync(Guid pendingContractId, PendingOrderApproveRequest request, CancellationToken cancellationToken = default)
    {
        LastApproveRequest = request;
        return Task.FromResult(ApprovePendingOrderResult);
    }

    public Task<PendingOrderDecisionResult> DeclinePendingOrderAsync(Guid pendingContractId, PendingOrderDeclineRequest request, CancellationToken cancellationToken = default)
    {
        LastDeclineRequest = request;
        return Task.FromResult(DeclinePendingOrderResult);
    }
}
