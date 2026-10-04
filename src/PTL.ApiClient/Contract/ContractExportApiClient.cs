using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.Contract;

namespace PTL.ApiClient;

public interface IContractExportApiClient
{
    Task<IReadOnlyList<SampleAddressResponse>> GetSampleAddressesAsync(Guid contractId, CancellationToken cancellationToken = default);

    Task<ContractRenewalResponse?> GetRenewalAsync(Guid contractId, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's document-export data endpoints.
public sealed class ContractExportApiClient(HttpClient httpClient) : IContractExportApiClient
{
    public async Task<IReadOnlyList<SampleAddressResponse>> GetSampleAddressesAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var addresses = await httpClient.GetFromJsonAsync<IReadOnlyList<SampleAddressResponse>>(
            $"/api/contracts/{contractId}/sample-addresses", cancellationToken);
        return addresses ?? [];
    }

    public async Task<ContractRenewalResponse?> GetRenewalAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/contracts/{contractId}/renewal", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContractRenewalResponse>(cancellationToken);
    }
}
