using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.CriterionItemType;

namespace PTL.ApiClient;

public interface ICriterionItemTypeApiClient
{
    Task<IReadOnlyList<CriterionItemTypeResponse>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);
    Task<CriterionItemTypeSaveResult> CreateAsync(CriterionItemTypeSaveRequest request, CancellationToken cancellationToken = default);
    Task<CriterionItemTypeSaveResult> UpdateAsync(Guid criterionItemTypeId, CriterionItemTypeSaveRequest request, CancellationToken cancellationToken = default);
    Task<CriterionItemTypeSaveResult> SetNoLongerInUseAsync(Guid criterionItemTypeId, CriterionItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default);
    Task<CriterionItemTypeDeleteResponse> DeleteAsync(Guid criterionItemTypeId, int yearId, CancellationToken cancellationToken = default);
}

public sealed class CriterionItemTypeApiClient(HttpClient httpClient) : ICriterionItemTypeApiClient
{
    public async Task<IReadOnlyList<CriterionItemTypeResponse>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<CriterionItemTypeResponse>>($"/api/criterion-item-types?yearId={yearId}", cancellationToken);
        return items ?? [];
    }

    public async Task<CriterionItemTypeSaveResult> CreateAsync(CriterionItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/criterion-item-types", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CriterionItemTypeSaveResult> UpdateAsync(Guid criterionItemTypeId, CriterionItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/criterion-item-types/{criterionItemTypeId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CriterionItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Criterion item was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CriterionItemTypeSaveResult> SetNoLongerInUseAsync(Guid criterionItemTypeId, CriterionItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsJsonAsync($"/api/criterion-item-types/{criterionItemTypeId}/no-longer-in-use", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CriterionItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Criterion item was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CriterionItemTypeDeleteResponse> DeleteAsync(Guid criterionItemTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/criterion-item-types/{criterionItemTypeId}?yearId={yearId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CriterionItemTypeDeleteResponse>(cancellationToken);
        return result ?? new CriterionItemTypeDeleteResponse(false, "The criterion item could not be removed.");
    }

    private static async Task<CriterionItemTypeSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new CriterionItemTypeSaveResult(false, null, errors);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await response.Content.ReadFromJsonAsync<CriterionItemTypeSaveResult>(cancellationToken);
            return conflict ?? new CriterionItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This criterion item cannot be changed."] });
        }

        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<CriterionItemTypeResponse>(cancellationToken);
        return new CriterionItemTypeSaveResult(true, saved, new Dictionary<string, string[]>());
    }
}
