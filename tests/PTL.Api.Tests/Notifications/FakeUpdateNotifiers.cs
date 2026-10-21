using PTL.Core.Customer;
using PTL.Core.Participant;

namespace PTL.Api.Tests.Notifications;

// Records the updates PendingCustomerUpdate/PendingParticipantUpdate approval asked to send, without any Notify or database work.
internal sealed class FakeCustomerUpdateNotifier : ICustomerUpdateNotifier
{
    public List<Guid> SentForUpdates { get; } = [];

    public Task SendForUpdateAsync(Guid pendingCustomerUpdateId, CancellationToken cancellationToken = default)
    {
        SentForUpdates.Add(pendingCustomerUpdateId);
        return Task.CompletedTask;
    }

    public Task SendUnsentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeParticipantUpdateNotifier : IParticipantUpdateNotifier
{
    public List<Guid> SentForUpdates { get; } = [];

    public Task SendForUpdateAsync(Guid pendingParticipantUpdateId, CancellationToken cancellationToken = default)
    {
        SentForUpdates.Add(pendingParticipantUpdateId);
        return Task.CompletedTask;
    }

    public Task SendUnsentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
