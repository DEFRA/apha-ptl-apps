using PTL.Core.InternalUser;
using CoreInternalUser = PTL.Core.InternalUser.InternalUser;

namespace PTL.Api.Tests.InternalUser;

internal sealed class FakeInternalUserRepository : IInternalUserRepository
{
    private readonly Dictionary<Guid, CoreInternalUser> _users = [];

    public List<(Guid UserId, Guid SsoIdInt)> UpdateCalls { get; } = [];

    public void Seed(CoreInternalUser user) => _users[user.UserId] = user;

    public Task<CoreInternalUser?> GetBySsoIdIntAsync(Guid ssoIdInt, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Values.FirstOrDefault(u => u.SsoIdInt == ssoIdInt));

    public Task<CoreInternalUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Values.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task UpdateSsoIdIntAsync(Guid userId, Guid ssoIdInt, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((userId, ssoIdInt));
        if (_users.TryGetValue(userId, out var user))
        {
            user.SsoIdInt = ssoIdInt;
        }

        return Task.CompletedTask;
    }
}
