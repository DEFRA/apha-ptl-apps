using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Scheme;

namespace PTL.ApiClient;

public interface ISchemeApiClient
{
    Task<SchemeResponse?> GetSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default);
    Task<SchemeSearchResponse> GetSchemesForYearAsync(SchemeSearchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchemeHistoryResponse>> GetSchemeHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default);
    Task<SchemeSaveResult> CreateSchemeAsync(CreateSchemeRequest request, CancellationToken cancellationToken = default);
    Task<SchemeSaveResult> UpdateSchemeAsync(Guid schemeId, UpdateSchemeRequest request, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's scheme endpoints, shared by every web front-end
// (PTL.InternalWeb, PTL.ExternalWeb, ...) - mirrors PTL.ApiClient.Contract.ContractApiClient.
public sealed class SchemeApiClient(HttpClient httpClient) : ISchemeApiClient
{
    public async Task<SchemeResponse?> GetSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/schemes/{schemeId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SchemeResponse>(cancellationToken);
    }

    public async Task<SchemeSearchResponse> GetSchemesForYearAsync(SchemeSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = $"year={request.YearId}&searchTerm={Uri.EscapeDataString(request.SearchTerm ?? string.Empty)}&page={request.Page}&pageSize={request.PageSize}";
        var result = await httpClient.GetFromJsonAsync<SchemeSearchResponse>($"/api/schemes?{query}", cancellationToken);
        return result ?? new SchemeSearchResponse([], 0, request.Page, request.PageSize);
    }

    public async Task<IReadOnlyList<SchemeHistoryResponse>> GetSchemeHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<SchemeHistoryResponse>>($"/api/schemes/families/{sharedId}/history", cancellationToken);
        return result ?? [];
    }

    public async Task<SchemeSaveResult> CreateSchemeAsync(CreateSchemeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/schemes", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<SchemeSaveResult> UpdateSchemeAsync(Guid schemeId, UpdateSchemeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/schemes/{schemeId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new SchemeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Scheme was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    private static async Task<SchemeSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new SchemeSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var scheme = await response.Content.ReadFromJsonAsync<SchemeResponse>(cancellationToken);
        return new SchemeSaveResult(true, scheme, new Dictionary<string, string[]>());
    }
}
