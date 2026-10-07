using PTL.ApiClient;
using PTL.Contracts.ExternalUser;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Test double for IExternalUserApiClient so ExternalUserResolver unit tests don't need a real HTTP call.
internal sealed class FakeExternalUserApiClient(ResolveExternalUserResponse response) : IExternalUserApiClient
{
    public ResolveExternalUserRequest? ReceivedRequest { get; private set; }

    public Task<ResolveExternalUserResponse> ResolveAsync(ResolveExternalUserRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedRequest = request;
        return Task.FromResult(response);
    }
}
