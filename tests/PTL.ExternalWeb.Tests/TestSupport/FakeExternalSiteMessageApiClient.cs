using PTL.ApiClient;
using PTL.Contracts.ExternalSiteMessage;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Test double for IExternalSiteMessageApiClient so the error pages' support email needs no real HTTP call.
internal sealed class FakeExternalSiteMessageApiClient(string supportEmailAddress, Exception? thrownException = null) : IExternalSiteMessageApiClient
{
    public const string DefaultSupportEmailAddress = "support@apha.test";

    public FakeExternalSiteMessageApiClient()
        : this(DefaultSupportEmailAddress)
    {
    }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<ExternalSiteMessageResponse> GetExternalSiteMessageAsync(CancellationToken cancellationToken = default)
    {
        ReceivedCancellationToken = cancellationToken;
        return thrownException is not null
            ? throw thrownException
            : Task.FromResult(new ExternalSiteMessageResponse(string.Empty, string.Empty, supportEmailAddress));
    }

    public Task<ExternalSiteMessageSaveResult> UpdateExternalSiteMessageAsync(ExternalSiteMessageSaveRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("PTL.ExternalWeb never updates the external site message.");
}
