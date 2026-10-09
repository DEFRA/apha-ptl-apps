using PTL.Core.ExternalSiteMessage;

namespace PTL.Api.Tests.ExternalSiteMessage;

internal sealed class FakeExternalSiteMessageRepository : IExternalSiteMessageRepository
{
    public PTL.Core.ExternalSiteMessage.ExternalSiteMessage Message { get; set; } = new();
    public PTL.Core.ExternalSiteMessage.ExternalSiteMessage? Updated { get; private set; }

    public Task<PTL.Core.ExternalSiteMessage.ExternalSiteMessage> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Message);

    public Task UpdateAsync(PTL.Core.ExternalSiteMessage.ExternalSiteMessage message, CancellationToken cancellationToken = default)
    {
        Updated = message;
        Message = message;
        return Task.CompletedTask;
    }
}
