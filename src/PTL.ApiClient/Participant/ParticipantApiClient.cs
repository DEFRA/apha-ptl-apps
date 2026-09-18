using System.Net;
using System.Net.Http.Json;
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

    public async Task<ParticipantResponse> CreateParticipantAsync(CreateParticipantRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/participants", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantResponse>(cancellationToken) ?? throw new InvalidOperationException("Participant creation response was empty.");
    }

    public async Task<ParticipantResponse?> UpdateParticipantAsync(Guid participantId, UpdateParticipantRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/participants/{participantId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantResponse>(cancellationToken);
    }
}
