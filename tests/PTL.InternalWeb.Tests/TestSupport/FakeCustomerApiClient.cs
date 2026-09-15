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

    public Task<IReadOnlyList<CustomerSummaryResponse>> GetCustomersAsync(CustomerStatusFilter status = CustomerStatusFilter.Active, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CustomerSummaryResponse>>(SearchResponse.Items);

    public Task<CustomerResponse?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerResponse);

    public Task<CustomerSearchResponse> SearchCustomersAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SearchResponse);

    public Task<CustomerSaveResult> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);

    public Task<CustomerSaveResult> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);

    public Task<CustomerSaveResult> DeactivateCustomerAsync(Guid customerId, Guid customerStatusId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);

    public Task<CustomerSaveResult> ReactivateCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);
}
