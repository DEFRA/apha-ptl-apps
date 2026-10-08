using PTL.Contracts.ExternalUser;

namespace PTL.ApiClient;

public interface IExternalUserApiClient
{
    Task<ResolveExternalUserResponse> ResolveAsync(ResolveExternalUserRequest request, CancellationToken cancellationToken = default);
}
