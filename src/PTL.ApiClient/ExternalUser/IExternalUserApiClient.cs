using PTL.Contracts.ExternalUser;

namespace PTL.ApiClient;

public interface IExternalUserApiClient
{
    Task<ResolveExternalUserResponse> ResolveAsync(ResolveExternalUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>The active labs the signed-in user (by CIDM contact id) can work on.</summary>
    Task<IReadOnlyList<ExternalLab>> GetLabsAsync(Guid ssoIdExt, CancellationToken cancellationToken = default);

    /// <summary>The lab to work on, only when it is one of the user's active labs; otherwise <see langword="null"/>.</summary>
    Task<ExternalLabContext?> GetLabAsync(Guid ssoIdExt, Guid participantId, CancellationToken cancellationToken = default);
}
