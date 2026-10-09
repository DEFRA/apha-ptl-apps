using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestConsultant;
using PTL.Contracts.Viewer;

namespace PTL.ApiClient;

public interface IViewerApiClient
{
    Task<IReadOnlyList<ViewerResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ViewerSaveResult> CreateAsync(ViewerSaveRequest request, CancellationToken cancellationToken = default);
    Task<ViewerSaveResult> UpdateAsync(Guid viewerId, ViewerSaveRequest request, CancellationToken cancellationToken = default);
    Task<ViewerDeleteResponse> DeleteAsync(Guid viewerId, CancellationToken cancellationToken = default);
    Task<GenerateLoginResponse> GenerateLoginAsync(Guid viewerId, CancellationToken cancellationToken = default);
}

public sealed class ViewerApiClient(HttpClient httpClient) : IViewerApiClient
{
    public async Task<IReadOnlyList<ViewerResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<ViewerResponse>>("/api/viewers", cancellationToken);
        return items ?? [];
    }

    public async Task<ViewerSaveResult> CreateAsync(ViewerSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/viewers", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<ViewerSaveResult> UpdateAsync(Guid viewerId, ViewerSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/viewers/{viewerId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ViewerSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Viewer was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<ViewerDeleteResponse> DeleteAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/viewers/{viewerId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ViewerDeleteResponse>(cancellationToken);
        return result ?? new ViewerDeleteResponse(false, "The removal did not return a result.");
    }

    public async Task<GenerateLoginResponse> GenerateLoginAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/api/viewers/{viewerId}/generate-login", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GenerateLoginResponse>(cancellationToken);
        return result ?? new GenerateLoginResponse(false, "The login could not be generated.");
    }

    private static async Task<ViewerSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new ViewerSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var viewer = await response.Content.ReadFromJsonAsync<ViewerResponse>(cancellationToken);
        return new ViewerSaveResult(true, viewer, new Dictionary<string, string[]>());
    }
}
