using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.CategoryItemType;

namespace PTL.ApiClient;

public interface ICategoryItemTypeApiClient
{
    Task<IReadOnlyList<CategoryItemTypeResponse>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);
    Task<CategoryItemTypeSaveResult> CreateAsync(CategoryItemTypeSaveRequest request, CancellationToken cancellationToken = default);
    Task<CategoryItemTypeSaveResult> UpdateAsync(Guid categoryItemTypeId, CategoryItemTypeSaveRequest request, CancellationToken cancellationToken = default);
    Task<CategoryItemTypeSaveResult> SetNoLongerInUseAsync(Guid categoryItemTypeId, CategoryItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default);
    Task<CategoryItemTypeDeleteResponse> DeleteAsync(Guid categoryItemTypeId, CancellationToken cancellationToken = default);
}

public sealed class CategoryItemTypeApiClient(HttpClient httpClient) : ICategoryItemTypeApiClient
{
    public async Task<IReadOnlyList<CategoryItemTypeResponse>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<CategoryItemTypeResponse>>($"/api/category-item-types?yearId={yearId}", cancellationToken);
        return items ?? [];
    }

    public async Task<CategoryItemTypeSaveResult> CreateAsync(CategoryItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/category-item-types", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CategoryItemTypeSaveResult> UpdateAsync(Guid categoryItemTypeId, CategoryItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/category-item-types/{categoryItemTypeId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CategoryItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Category item was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CategoryItemTypeSaveResult> SetNoLongerInUseAsync(Guid categoryItemTypeId, CategoryItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsJsonAsync($"/api/category-item-types/{categoryItemTypeId}/no-longer-in-use", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CategoryItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Category item was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CategoryItemTypeDeleteResponse> DeleteAsync(Guid categoryItemTypeId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/category-item-types/{categoryItemTypeId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CategoryItemTypeDeleteResponse>(cancellationToken);
        return result ?? new CategoryItemTypeDeleteResponse(false, "The category item could not be removed.");
    }

    private static async Task<CategoryItemTypeSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new CategoryItemTypeSaveResult(false, null, errors);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await response.Content.ReadFromJsonAsync<CategoryItemTypeSaveResult>(cancellationToken);
            return conflict ?? new CategoryItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This category item cannot be changed."] });
        }

        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<CategoryItemTypeResponse>(cancellationToken);
        return new CategoryItemTypeSaveResult(true, saved, new Dictionary<string, string[]>());
    }
}
