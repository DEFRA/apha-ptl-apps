using System.Net.Http.Json;
using PTL.Contracts.Contract;

namespace PTL.ApiClient;

/// <summary>Whole-dataset reads behind the four Exports screens.</summary>
public interface IBulkExportApiClient
{
    Task<IReadOnlyList<BulkContractResponse>> GetContractsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SampleAddressResponse>> GetSampleAddressesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContractRenewalResponse>> GetRenewalsAsync(bool nonUk, CancellationToken cancellationToken = default);
}

public sealed class BulkExportApiClient(HttpClient httpClient) : IBulkExportApiClient
{
    public async Task<IReadOnlyList<BulkContractResponse>> GetContractsAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<BulkContractResponse>>("/api/exports/contracts", cancellationToken) ?? [];

    public async Task<IReadOnlyList<SampleAddressResponse>> GetSampleAddressesAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<SampleAddressResponse>>("/api/exports/sample-addresses", cancellationToken) ?? [];

    public async Task<IReadOnlyList<ContractRenewalResponse>> GetRenewalsAsync(bool nonUk, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<ContractRenewalResponse>>(
            $"/api/exports/renewals?nonUk={(nonUk ? "true" : "false")}", cancellationToken) ?? [];
}
