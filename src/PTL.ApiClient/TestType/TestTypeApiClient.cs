using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestType;

namespace PTL.ApiClient;

public interface ITestTypeApiClient
{
    Task<IReadOnlyList<TestTypeResponse>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);
    Task<TestTypeSaveResult> CreateAsync(TestTypeSaveRequest request, CancellationToken cancellationToken = default);
    Task<TestTypeSaveResult> UpdateAsync(Guid testTypeId, TestTypeSaveRequest request, CancellationToken cancellationToken = default);
    Task<TestTypeSaveResult> SetNoLongerInUseAsync(Guid testTypeId, TestTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default);
    Task<TestTypeDeleteResponse> DeleteAsync(Guid testTypeId, int yearId, CancellationToken cancellationToken = default);
}

public sealed class TestTypeApiClient(HttpClient httpClient) : ITestTypeApiClient
{
    public async Task<IReadOnlyList<TestTypeResponse>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<TestTypeResponse>>($"/api/test-types?yearId={yearId}", cancellationToken);
        return items ?? [];
    }

    public async Task<TestTypeSaveResult> CreateAsync(TestTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/test-types", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<TestTypeSaveResult> UpdateAsync(Guid testTypeId, TestTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/test-types/{testTypeId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new TestTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Test type was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<TestTypeSaveResult> SetNoLongerInUseAsync(Guid testTypeId, TestTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsJsonAsync($"/api/test-types/{testTypeId}/no-longer-in-use", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new TestTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Test type was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<TestTypeDeleteResponse> DeleteAsync(Guid testTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/test-types/{testTypeId}?yearId={yearId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TestTypeDeleteResponse>(cancellationToken);
        return result ?? new TestTypeDeleteResponse(false, "The test type could not be removed.");
    }

    private static async Task<TestTypeSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new TestTypeSaveResult(false, null, errors);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await response.Content.ReadFromJsonAsync<TestTypeSaveResult>(cancellationToken);
            return conflict ?? new TestTypeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["This test type cannot be changed."] });
        }

        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<TestTypeResponse>(cancellationToken);
        return new TestTypeSaveResult(true, saved, new Dictionary<string, string[]>());
    }
}
