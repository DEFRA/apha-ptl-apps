using System.Net.Http.Json;
using PTL.Contracts.Contract;

namespace PTL.ApiClient;

public interface IContractRenewalApiClient
{
    Task<RenewableContractsResponse> GetRenewableContractsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<RenewableContractItemsResponse> GetRenewableItemsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<RenewContractResponse> RenewContractsAsync(Guid customerId, RenewContractRequest request, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's "Renew Contracts" endpoints.
public sealed class ContractRenewalApiClient(HttpClient httpClient) : IContractRenewalApiClient
{
    public async Task<RenewableContractsResponse> GetRenewableContractsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<RenewableContractsResponse>($"/api/customers/{customerId}/contracts/renewable-contracts", cancellationToken);
        return result ?? new RenewableContractsResponse(false, null, [], []);
    }

    public async Task<RenewableContractItemsResponse> GetRenewableItemsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<RenewableContractItemsResponse>($"/api/customers/{customerId}/contracts/renewable-items", cancellationToken);
        return result ?? new RenewableContractItemsResponse([]);
    }

    public async Task<RenewContractResponse> RenewContractsAsync(Guid customerId, RenewContractRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/customers/{customerId}/contracts/renew", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RenewContractResponse>(cancellationToken);
        return result ?? new RenewContractResponse(false, null, "The request was invalid.");
    }
}
