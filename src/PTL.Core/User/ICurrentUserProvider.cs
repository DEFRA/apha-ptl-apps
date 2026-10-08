namespace PTL.Core.User;

// [NEEDS INVESTIGATION] Stub only - there is no real authenticated-user concept anywhere in this
// phase (authentication/authorization are out of scope). Once internal auth (Entra ID/OIDC per
// the HLD) is wired in, implement this by reading the signed-in user's identity claim from
// HttpContext and resolving it to a PTL.Core.User.User.UserId. Until then this always returns
// null, meaning the "cannot remove your own Admin role" business rule in UserRoleService can
// never actually trigger in the running app - the rule itself is fully implemented and unit
// tested with a fake, only the "who is logged in" lookup is stubbed.
public interface ICurrentUserProvider
{
    Task<Guid?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);
}

public sealed class StubCurrentUserProvider : ICurrentUserProvider
{
    public Task<Guid?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
}
