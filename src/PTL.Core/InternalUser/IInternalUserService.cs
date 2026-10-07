namespace PTL.Core.InternalUser;

/// <summary>
/// Resolves an Entra ID-authenticated internal user against tblUsers. Unlike the external-user CIDM
/// flow, there is no auto-create and no "limited access" fallback here - a user with no matching
/// tblUsers row is denied sign-in entirely.
/// </summary>
public interface IInternalUserService
{
    Task<InternalUserResolutionResult> ResolveAsync(
        Guid ssoIdInt,
        string username,
        CancellationToken cancellationToken = default);
}
