using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.Customer;

namespace PTL.ApiClient;

public interface ICustomerDetailsEditApiClient
{
    Task<CustomerDetailsEditResponse?> GetAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<CustomerDetailsEditSaveResult> SubmitAsync(Guid participantId, CustomerDetailsEditSaveRequest request, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's participant-scoped customer-details-edit
// endpoint - PTL.ExternalWeb only (legacy EditCustomerDetails.aspx self-service).
public sealed class CustomerDetailsEditApiClient(HttpClient httpClient) : ICustomerDetailsEditApiClient
{
    public async Task<CustomerDetailsEditResponse?> GetAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/customer-details-edit/{participantId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerDetailsEditResponse>(cancellationToken);
    }

    public async Task<CustomerDetailsEditSaveResult> SubmitAsync(Guid participantId, CustomerDetailsEditSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/customer-details-edit/{participantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CustomerDetailsEditSaveResult>(cancellationToken);
        return result ?? new CustomerDetailsEditSaveResult(false, new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] });
    }
}
