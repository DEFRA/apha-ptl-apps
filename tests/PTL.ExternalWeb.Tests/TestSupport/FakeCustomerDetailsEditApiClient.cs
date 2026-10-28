using PTL.ApiClient;
using PTL.Contracts.Customer;

namespace PTL.ExternalWeb.Tests.TestSupport;

internal sealed class FakeCustomerDetailsEditApiClient : ICustomerDetailsEditApiClient
{
    public CustomerDetailsEditResponse? Details { get; set; }
    public CustomerDetailsEditSaveResult SubmitResult { get; set; } = new(true, new Dictionary<string, string[]>());
    public CustomerDetailsEditSaveRequest? LastSubmitRequest { get; private set; }

    public Task<CustomerDetailsEditResponse?> GetAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Details);

    public Task<CustomerDetailsEditSaveResult> SubmitAsync(Guid participantId, CustomerDetailsEditSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastSubmitRequest = request;
        return Task.FromResult(SubmitResult);
    }
}
