using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.ExternalSiteMessage;

namespace PTL.ApiClient;

public interface IExternalSiteMessageApiClient
{
    Task<ExternalSiteMessageResponse> GetExternalSiteMessageAsync(CancellationToken cancellationToken = default);
    Task<ExternalSiteMessageSaveResult> UpdateExternalSiteMessageAsync(ExternalSiteMessageSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed class ExternalSiteMessageApiClient(HttpClient httpClient) : IExternalSiteMessageApiClient
{
    public async Task<ExternalSiteMessageResponse> GetExternalSiteMessageAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<ExternalSiteMessageResponse>("/api/external-site-message", cancellationToken);
        return response ?? new ExternalSiteMessageResponse(string.Empty, string.Empty, string.Empty);
    }

    public async Task<ExternalSiteMessageSaveResult> UpdateExternalSiteMessageAsync(ExternalSiteMessageSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync("/api/external-site-message", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new ExternalSiteMessageSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var message = await response.Content.ReadFromJsonAsync<ExternalSiteMessageResponse>(cancellationToken);
        return new ExternalSiteMessageSaveResult(true, message, new Dictionary<string, string[]>());
    }
}
