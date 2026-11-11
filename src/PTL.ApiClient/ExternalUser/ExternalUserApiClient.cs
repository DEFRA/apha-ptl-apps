using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.ExternalUser;

namespace PTL.ApiClient;

public sealed class ExternalUserApiClient(HttpClient httpClient) : IExternalUserApiClient
{
    public async Task<ResolveExternalUserResponse> ResolveAsync(ResolveExternalUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/external-users/resolve", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ResolveExternalUserResponse>(cancellationToken);
        return result ?? ResolveExternalUserResponse.Denied(ExternalUserResolutionStatus.NotFound, request.DisplayName);
    }

    public async Task<IReadOnlyList<ExternalLab>> GetLabsAsync(Guid ssoIdExt, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/external-users/{ssoIdExt}/labs", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<ExternalLab>>(cancellationToken) ?? [];
    }

    public async Task<ExternalLabContext?> GetLabAsync(Guid ssoIdExt, Guid participantId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/external-users/{ssoIdExt}/labs/{participantId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExternalLabContext>(cancellationToken);
    }
}
