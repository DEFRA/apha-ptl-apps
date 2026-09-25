using System.Net.Http.Json;
using PTL.Contracts.Contract;

namespace PTL.ApiClient;

public interface IImportPermitApiClient
{
    Task<IReadOnlyList<ImportPermitResponse>> GetImportPermitsAsync(Guid contractId, CancellationToken cancellationToken = default);

    Task<bool> UpdateImportPermitAsync(Guid participantSchemeId, UpdateImportPermitRequest request, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's Import Permit(s) endpoints (see ImportPermit.aspx).
public sealed class ImportPermitApiClient(HttpClient httpClient) : IImportPermitApiClient
{
    public async Task<IReadOnlyList<ImportPermitResponse>> GetImportPermitsAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var permits = await httpClient.GetFromJsonAsync<IReadOnlyList<ImportPermitResponse>>($"/api/contracts/{contractId}/import-permits", cancellationToken);
        return permits ?? [];
    }

    public async Task<bool> UpdateImportPermitAsync(Guid participantSchemeId, UpdateImportPermitRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/contracts/import-permits/{participantSchemeId}", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
