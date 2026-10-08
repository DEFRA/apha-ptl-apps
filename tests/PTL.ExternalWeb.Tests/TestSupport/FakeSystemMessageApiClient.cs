using PTL.ApiClient;
using PTL.Contracts.SystemMessage;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Test double for ISystemMessageApiClient so controller unit tests don't need a real HTTP call.
internal sealed class FakeSystemMessageApiClient(string? importantMessage = null) : ISystemMessageApiClient
{
    public Task<GetImportantMessageResponse> GetImportantMessageAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new GetImportantMessageResponse(importantMessage));
}
