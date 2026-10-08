using PTL.Contracts.InternalUser;

namespace PTL.ApiClient;

public interface IInternalUserApiClient
{
    Task<ResolveInternalUserResponse> ResolveAsync(ResolveInternalUserRequest request, CancellationToken cancellationToken = default);
}
