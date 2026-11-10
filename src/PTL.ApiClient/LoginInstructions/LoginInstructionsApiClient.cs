using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.LoginInstructions;

namespace PTL.ApiClient;

public interface ILoginInstructionsApiClient
{
    Task<LoginInstructionsResponse> SendToParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<LoginInstructionsResponse> SendToViewerAsync(Guid viewerId, CancellationToken cancellationToken = default);
    Task<LoginInstructionsResponse> SendToExternalTestConsultantAsync(Guid externalTestConsultantId, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's login instructions endpoints.
public sealed class LoginInstructionsApiClient(HttpClient httpClient) : ILoginInstructionsApiClient
{
    public Task<LoginInstructionsResponse> SendToParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        PostAsync($"/api/login-instructions/participants/{participantId}", "The participant was not found.", cancellationToken);

    public Task<LoginInstructionsResponse> SendToViewerAsync(Guid viewerId, CancellationToken cancellationToken = default) =>
        PostAsync($"/api/login-instructions/viewers/{viewerId}", "The viewer was not found.", cancellationToken);

    public Task<LoginInstructionsResponse> SendToExternalTestConsultantAsync(Guid externalTestConsultantId, CancellationToken cancellationToken = default) =>
        PostAsync($"/api/login-instructions/external-test-consultants/{externalTestConsultantId}", "The test consultant was not found.", cancellationToken);

    private async Task<LoginInstructionsResponse> PostAsync(string url, string notFoundMessage, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(url, content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new LoginInstructionsResponse(false, notFoundMessage);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginInstructionsResponse>(cancellationToken)
            ?? new LoginInstructionsResponse(false, "The login instructions email could not be sent. Please try again.");
    }
}
