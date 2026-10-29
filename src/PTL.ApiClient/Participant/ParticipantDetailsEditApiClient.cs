using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.Participant;

namespace PTL.ApiClient;

public interface IParticipantDetailsEditApiClient
{
    Task<ParticipantDetailsEditResponse?> GetAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<ParticipantDetailsEditSaveResult> SubmitAsync(Guid participantId, ParticipantDetailsEditSaveRequest request, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's participant-scoped participant-details-edit
// endpoint - PTL.ExternalWeb only (legacy EditParticipantDetails.aspx self-service).
public sealed class ParticipantDetailsEditApiClient(HttpClient httpClient) : IParticipantDetailsEditApiClient
{
    public async Task<ParticipantDetailsEditResponse?> GetAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/participant-details-edit/{participantId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantDetailsEditResponse>(cancellationToken);
    }

    public async Task<ParticipantDetailsEditSaveResult> SubmitAsync(Guid participantId, ParticipantDetailsEditSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/participant-details-edit/{participantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ParticipantDetailsEditSaveResult>(cancellationToken);
        return result ?? new ParticipantDetailsEditSaveResult(false, new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] });
    }
}
