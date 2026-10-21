using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestMethodItemType;

namespace PTL.ApiClient;

public interface ITestMethodItemTypeApiClient
{
    Task<IReadOnlyList<TestMethodItemTypeResponse>> GetByYearAsync(int yearId, string? search = null, CancellationToken cancellationToken = default);
    Task<TestMethodItemTypeSaveResult> CreateAsync(TestMethodItemTypeSaveRequest request, CancellationToken cancellationToken = default);
    Task<TestMethodItemTypeSaveResult> UpdateAsync(Guid testMethodItemTypeId, TestMethodItemTypeSaveRequest request, CancellationToken cancellationToken = default);
    Task<TestMethodItemTypeSaveResult> SetNoLongerInUseAsync(Guid testMethodItemTypeId, TestMethodItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default);
    Task<TestMethodItemTypeDeleteResponse> DeleteAsync(Guid testMethodItemTypeId, int yearId, CancellationToken cancellationToken = default);
}

public sealed class TestMethodItemTypeApiClient(HttpClient httpClient) : ITestMethodItemTypeApiClient
{
    public async Task<IReadOnlyList<TestMethodItemTypeResponse>> GetByYearAsync(int yearId, string? search = null, CancellationToken cancellationToken = default)
    {
        var url = $"/api/test-method-item-types?yearId={yearId}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<TestMethodItemTypeResponse>>(url, cancellationToken);
        return items ?? [];
    }

    public async Task<TestMethodItemTypeSaveResult> CreateAsync(TestMethodItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/test-method-item-types", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<TestMethodItemTypeSaveResult> UpdateAsync(Guid testMethodItemTypeId, TestMethodItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/test-method-item-types/{testMethodItemTypeId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new TestMethodItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Test method item was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<TestMethodItemTypeSaveResult> SetNoLongerInUseAsync(Guid testMethodItemTypeId, TestMethodItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsJsonAsync($"/api/test-method-item-types/{testMethodItemTypeId}/no-longer-in-use", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new TestMethodItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Test method item was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<TestMethodItemTypeDeleteResponse> DeleteAsync(Guid testMethodItemTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/test-method-item-types/{testMethodItemTypeId}?yearId={yearId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TestMethodItemTypeDeleteResponse>(cancellationToken);
        return result ?? new TestMethodItemTypeDeleteResponse(false, "The test method item could not be removed.");
    }

    private static async Task<TestMethodItemTypeSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new TestMethodItemTypeSaveResult(false, null, errors);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await response.Content.ReadFromJsonAsync<TestMethodItemTypeSaveResult>(cancellationToken);
            return conflict ?? new TestMethodItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This test method item cannot be changed."] });
        }

        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<TestMethodItemTypeResponse>(cancellationToken);
        return new TestMethodItemTypeSaveResult(true, saved, new Dictionary<string, string[]>());
    }
}
