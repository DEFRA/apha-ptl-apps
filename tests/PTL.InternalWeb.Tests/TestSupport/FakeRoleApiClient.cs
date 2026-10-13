using PTL.ApiClient;
using PTL.Contracts.User;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeRoleApiClient : IRoleApiClient
{
    public IReadOnlyList<RoleResponse> Roles { get; set; } = [];

    public Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Roles);
}
