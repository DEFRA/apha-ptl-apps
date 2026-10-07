using System.Net.Http.Json;
using PTL.Contracts.InternalUser;

namespace PTL.ApiClient;

public sealed class InternalUserApiClient(HttpClient httpClient) : IInternalUserApiClient
{
    public async Task<ResolveInternalUserResponse> ResolveAsync(ResolveInternalUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/internal-users/resolve", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ResolveInternalUserResponse>(cancellationToken);
        return result ?? new ResolveInternalUserResponse(false, null, string.Empty, string.Empty, []);
    }
}
