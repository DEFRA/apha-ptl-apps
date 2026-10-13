using PTL.ApiClient;
using PTL.Contracts.ExternalSiteMessage;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IExternalSiteMessageApiClient so SystemAdministrationController tests don't
// need a real HTTP call to PTL.Api.
internal sealed class FakeExternalSiteMessageApiClient : IExternalSiteMessageApiClient
{
    public ExternalSiteMessageResponse Message { get; set; } = new(string.Empty, string.Empty, string.Empty);
    public ExternalSiteMessageSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public ExternalSiteMessageSaveRequest? LastSaveRequest { get; private set; }

    public Task<ExternalSiteMessageResponse> GetExternalSiteMessageAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Message);

    public Task<ExternalSiteMessageSaveResult> UpdateExternalSiteMessageAsync(ExternalSiteMessageSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastSaveRequest = request;
        return Task.FromResult(SaveResult);
    }
}
