using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Participant;

namespace PTL.ApiClient;

public sealed class ParticipantApiClient(HttpClient httpClient) : IParticipantApiClient
{
    public async Task<IReadOnlyList<ParticipantSummaryResponse>> GetParticipantsAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var participants = await httpClient.GetFromJsonAsync<IReadOnlyList<ParticipantSummaryResponse>>(
            $"/api/customers/{customerId}/participants?includeInactive={includeInactive}", cancellationToken);
        return participants ?? [];
    }

    public async Task<ParticipantResponse?> GetParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/participants/{participantId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantResponse>(cancellationToken);
    }

    public async Task<ParticipantSearchResponse> SearchParticipantsAsync(ParticipantSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = $"searchTerm={Uri.EscapeDataString(request.SearchTerm ?? string.Empty)}&includeInactive={request.IncludeInactive}&page={request.Page}&pageSize={request.PageSize}";
        var result = await httpClient.GetFromJsonAsync<ParticipantSearchResponse>($"/api/customers/{request.CustomerId}/participants/search?{query}", cancellationToken);
        return result ?? new ParticipantSearchResponse([], 0, request.Page, request.PageSize);
    }

    public async Task<ParticipantSaveResult> CreateParticipantAsync(ParticipantRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/participants", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<ParticipantSaveResult> UpdateParticipantAsync(Guid participantId, ParticipantRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/participants/{participantId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ParticipantSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Participant was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    private static async Task<ParticipantSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new ParticipantSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var participant = await response.Content.ReadFromJsonAsync<ParticipantResponse>(cancellationToken);
        return new ParticipantSaveResult(true, participant, new Dictionary<string, string[]>());
    }
}
