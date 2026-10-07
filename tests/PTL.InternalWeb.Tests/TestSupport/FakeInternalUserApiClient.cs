using PTL.ApiClient;
using PTL.Contracts.InternalUser;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IInternalUserApiClient so InternalUserResolver unit tests don't need a real HTTP call.
internal sealed class FakeInternalUserApiClient(ResolveInternalUserResponse response) : IInternalUserApiClient
{
    public ResolveInternalUserRequest? ReceivedRequest { get; private set; }

    public Task<ResolveInternalUserResponse> ResolveAsync(ResolveInternalUserRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedRequest = request;
        return Task.FromResult(response);
    }
}
