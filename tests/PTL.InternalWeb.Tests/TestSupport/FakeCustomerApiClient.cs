using PTL.ApiClient;
using PTL.Contracts.Customer;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ICustomerApiClient so CustomerController tests don't need a real HTTP call
// to PTL.Api. Configure each scripted response via the constructor/settable properties.
internal sealed class FakeCustomerApiClient : ICustomerApiClient
{
    public CustomerSearchResponse SearchResponse { get; set; } = new([], 0, 1, 20);
    public CustomerResponse? CustomerResponse { get; set; }
    public CustomerSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public IReadOnlyList<PendingCustomerUpdateSummaryResponse> PendingCustomerUpdates { get; set; } = [];
    public PendingCustomerUpdateComparisonResponse? PendingCustomerUpdateComparison { get; set; }
    public PendingCustomerUpdateDecisionResult ApprovePendingCustomerUpdateResult { get; set; } = new(true, false, new Dictionary<string, string[]>());
    public bool DeclinePendingCustomerUpdateResult { get; set; } = true;
    public PendingCustomerUpdateSaveRequest? LastApproveRequest { get; private set; }
    public CustomerSaveRequest? LastSaveRequest { get; private set; }

    public Task<IReadOnlyList<CustomerSummaryResponse>> GetCustomersAsync(CustomerStatusFilter status = CustomerStatusFilter.Active, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CustomerSummaryResponse>>(SearchResponse.Items);

    public Task<CustomerResponse?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerResponse);

    public Task<CustomerSearchResponse> SearchCustomersAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SearchResponse);

    public Task<CustomerSaveResult> CreateCustomerAsync(CustomerSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastSaveRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<CustomerSaveResult> UpdateCustomerAsync(Guid customerId, CustomerSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastSaveRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<IReadOnlyList<PendingCustomerUpdateSummaryResponse>> GetPendingCustomerUpdatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(PendingCustomerUpdates);

    public Task<PendingCustomerUpdateComparisonResponse?> GetPendingCustomerUpdateAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PendingCustomerUpdateComparison);

    public Task<PendingCustomerUpdateDecisionResult> ApprovePendingCustomerUpdateAsync(Guid customerId, PendingCustomerUpdateSaveRequest? request = null, CancellationToken cancellationToken = default)
    {
        LastApproveRequest = request;
        return Task.FromResult(ApprovePendingCustomerUpdateResult);
    }

    public Task<bool> DeclinePendingCustomerUpdateAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(DeclinePendingCustomerUpdateResult);
}
