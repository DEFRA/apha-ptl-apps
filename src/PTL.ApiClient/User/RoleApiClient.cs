using System.Net.Http.Json;
using PTL.Contracts.User;

namespace PTL.ApiClient;

public interface IRoleApiClient
{
    Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default);
}

public sealed class RoleApiClient(HttpClient httpClient) : IRoleApiClient
{
    public async Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<RoleResponse>>("/api/roles", cancellationToken);
        return items ?? [];
    }
}
