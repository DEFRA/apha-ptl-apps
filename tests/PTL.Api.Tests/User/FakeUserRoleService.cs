using PTL.Core.User;

namespace PTL.Api.Tests.User;

internal sealed class FakeUserRoleService : IUserRoleService
{
    public IReadOnlyList<Role> Roles { get; set; } = [];
    public IReadOnlyList<UserRoleRow> Grid { get; set; } = [];
    public SetUserRolesResult SetResult { get; set; } = new(true, null);
    public List<(Guid UserId, IReadOnlyList<Guid> RoleIds)> SetCalls { get; } = [];

    public Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Roles);

    public Task<IReadOnlyList<UserRoleRow>> GetUserRoleGridAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Grid);

    public Task<SetUserRolesResult> SetUserRolesAsync(Guid userId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        SetCalls.Add((userId, roleIds));
        return Task.FromResult(SetResult);
    }
}
