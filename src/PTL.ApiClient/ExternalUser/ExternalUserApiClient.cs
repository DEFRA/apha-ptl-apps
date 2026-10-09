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
        return result ?? new ResolveExternalUserResponse(request.DisplayName, [], null, null, null, null);
    }
}
