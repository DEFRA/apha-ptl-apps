using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestConsultant;

namespace PTL.ApiClient;

public interface IExternalTestConsultantApiClient
{
    Task<IReadOnlyList<ExternalTestConsultantResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ExternalTestConsultantSaveResult> CreateAsync(ExternalTestConsultantSaveRequest request, CancellationToken cancellationToken = default);
    Task<ExternalTestConsultantSaveResult> UpdateAsync(Guid externalTestConsultantId, ExternalTestConsultantSaveRequest request, CancellationToken cancellationToken = default);
    Task<ExternalTestConsultantResponse?> SetStatusAsync(Guid externalTestConsultantId, bool isInactive, CancellationToken cancellationToken = default);
    Task<GenerateLoginResponse> GenerateLoginAsync(Guid externalTestConsultantId, CancellationToken cancellationToken = default);
}

public sealed class ExternalTestConsultantApiClient(HttpClient httpClient) : IExternalTestConsultantApiClient
{
    public async Task<IReadOnlyList<ExternalTestConsultantResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<ExternalTestConsultantResponse>>("/api/external-test-consultants", cancellationToken);
        return items ?? [];
    }

    public async Task<ExternalTestConsultantSaveResult> CreateAsync(ExternalTestConsultantSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/external-test-consultants", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<ExternalTestConsultantSaveResult> UpdateAsync(Guid externalTestConsultantId, ExternalTestConsultantSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/external-test-consultants/{externalTestConsultantId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ExternalTestConsultantSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Test consultant was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<ExternalTestConsultantResponse?> SetStatusAsync(Guid externalTestConsultantId, bool isInactive, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/external-test-consultants/{externalTestConsultantId}/status", new ExternalTestConsultantStatusRequest(isInactive), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExternalTestConsultantResponse>(cancellationToken);
    }

    public async Task<GenerateLoginResponse> GenerateLoginAsync(Guid externalTestConsultantId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/api/external-test-consultants/{externalTestConsultantId}/generate-login", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GenerateLoginResponse>(cancellationToken);
        return result ?? new GenerateLoginResponse(false, "The login could not be generated.");
    }

    private static async Task<ExternalTestConsultantSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new ExternalTestConsultantSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var testConsultant = await response.Content.ReadFromJsonAsync<ExternalTestConsultantResponse>(cancellationToken);
        return new ExternalTestConsultantSaveResult(true, testConsultant, new Dictionary<string, string[]>());
    }
}
