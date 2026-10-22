using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestResultItemType;

namespace PTL.ApiClient;

public interface ITestResultItemTypeApiClient
{
    Task<IReadOnlyList<TestResultItemTypeResponse>> GetByYearAsync(int yearId, string? search = null, CancellationToken cancellationToken = default);
    Task<TestResultItemTypeSaveResult> CreateAsync(TestResultItemTypeSaveRequest request, CancellationToken cancellationToken = default);
    Task<TestResultItemTypeSaveResult> UpdateAsync(Guid testResultItemTypeId, TestResultItemTypeSaveRequest request, CancellationToken cancellationToken = default);
    Task<TestResultItemTypeSaveResult> SetNoLongerInUseAsync(Guid testResultItemTypeId, TestResultItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default);
    Task<TestResultItemTypeDeleteResponse> DeleteAsync(Guid testResultItemTypeId, int yearId, CancellationToken cancellationToken = default);
}

public sealed class TestResultItemTypeApiClient(HttpClient httpClient) : ITestResultItemTypeApiClient
{
    public async Task<IReadOnlyList<TestResultItemTypeResponse>> GetByYearAsync(int yearId, string? search = null, CancellationToken cancellationToken = default)
    {
        var url = $"/api/test-result-item-types?yearId={yearId}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }

        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<TestResultItemTypeResponse>>(url, cancellationToken);
        return items ?? [];
    }

    public async Task<TestResultItemTypeSaveResult> CreateAsync(TestResultItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/test-result-item-types", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<TestResultItemTypeSaveResult> UpdateAsync(Guid testResultItemTypeId, TestResultItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/test-result-item-types/{testResultItemTypeId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new TestResultItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Test result item was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<TestResultItemTypeSaveResult> SetNoLongerInUseAsync(Guid testResultItemTypeId, TestResultItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsJsonAsync($"/api/test-result-item-types/{testResultItemTypeId}/no-longer-in-use", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new TestResultItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Test result item was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<TestResultItemTypeDeleteResponse> DeleteAsync(Guid testResultItemTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/test-result-item-types/{testResultItemTypeId}?yearId={yearId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TestResultItemTypeDeleteResponse>(cancellationToken);
        return result ?? new TestResultItemTypeDeleteResponse(false, "The test result item could not be removed.");
    }

    private static async Task<TestResultItemTypeSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new TestResultItemTypeSaveResult(false, null, errors);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await response.Content.ReadFromJsonAsync<TestResultItemTypeSaveResult>(cancellationToken);
            return conflict ?? new TestResultItemTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This test result item cannot be changed."] });
        }

        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<TestResultItemTypeResponse>(cancellationToken);
        return new TestResultItemTypeSaveResult(true, saved, new Dictionary<string, string[]>());
    }
}
