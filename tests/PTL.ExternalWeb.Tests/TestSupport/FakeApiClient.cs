using PTL.ApiClient;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Test double for IApiClient so controller unit tests don't need a real HTTP call.
internal sealed class FakeApiClient(ApiHealthResponse? response = null) : IApiClient
{
    private readonly ApiHealthResponse? _response = response ?? new ApiHealthResponse("Healthy", 1, DateTime.UtcNow);

    public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_response);
}
