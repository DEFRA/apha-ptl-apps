using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Participant;

namespace PTL.ApiClient;

public interface IParticipantSchemeApiClient
{
    Task<ParticipantSchemeResponse?> GetParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default);
    Task<ParticipantSchemeSaveResult> CreateParticipantSchemeAsync(CreateParticipantSchemeRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantSchemeSaveResult> UpdateParticipantSchemeAsync(Guid participantSchemeId, UpdateParticipantSchemeRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantSchemeSaveResult> DeleteParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's participant-scheme endpoints - mirrors
// PTL.ApiClient.Contract.ContractApiClient.
public sealed class ParticipantSchemeApiClient(HttpClient httpClient) : IParticipantSchemeApiClient
{
    public async Task<ParticipantSchemeResponse?> GetParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/participant-schemes/{participantSchemeId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantSchemeResponse>(cancellationToken);
    }

    public async Task<ParticipantSchemeSaveResult> CreateParticipantSchemeAsync(CreateParticipantSchemeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/participant-schemes", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<ParticipantSchemeSaveResult> UpdateParticipantSchemeAsync(Guid participantSchemeId, UpdateParticipantSchemeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/participant-schemes/{participantSchemeId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ParticipantSchemeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Participant scheme was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<ParticipantSchemeSaveResult> DeleteParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/participant-schemes/{participantSchemeId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ParticipantSchemeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Participant scheme was not found."] });
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return await ToSaveResultAsync(response, cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        return new ParticipantSchemeSaveResult(true, null, new Dictionary<string, string[]>());
    }

    private static async Task<ParticipantSchemeSaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new ParticipantSchemeSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var participantScheme = await response.Content.ReadFromJsonAsync<ParticipantSchemeResponse>(cancellationToken);
        return new ParticipantSchemeSaveResult(true, participantScheme, new Dictionary<string, string[]>());
    }
}
