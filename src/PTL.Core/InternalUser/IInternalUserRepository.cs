namespace PTL.Core.InternalUser;

public interface IInternalUserRepository
{
    /// <summary>Reads the user row (and role list) matching an Entra object id via <c>sppAuthenticate</c>.</summary>
    Task<InternalUser?> GetBySsoIdIntAsync(Guid ssoIdInt, CancellationToken cancellationToken = default);

    /// <summary>Reads the user row (and role list) matching a Windows username via <c>sppAuthenticate</c>, used only as an Entra fallback.</summary>
    Task<InternalUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Backfills fldSsoIdInt on a row previously matched by username, via <c>spuUserSsoIdInt</c>.</summary>
    Task UpdateSsoIdIntAsync(Guid userId, Guid ssoIdInt, CancellationToken cancellationToken = default);
}
