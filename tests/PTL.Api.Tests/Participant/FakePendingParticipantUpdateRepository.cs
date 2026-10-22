using PTL.Core.Participant;

namespace PTL.Api.Tests.Participant;

// In-memory IPendingParticipantUpdateRepository test double so ParticipantService can be tested
// without a real database or the spgaPendingParticipantDetailsEditInfo/
// spgPendingParticipantDetailsEditByParticipantID/spdPendingParticipantDetailsEditByParticipantID
// stored procedures.
internal sealed class FakePendingParticipantUpdateRepository : IPendingParticipantUpdateRepository
{
    private readonly Dictionary<Guid, PendingParticipantUpdate> _byParticipantId = [];

    public void Seed(PendingParticipantUpdate pendingUpdate) => _byParticipantId[pendingUpdate.ParticipantId] = pendingUpdate;

    public Task<IReadOnlyList<PendingParticipantUpdateSummaryEntity>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PendingParticipantUpdateSummaryEntity> summaries = _byParticipantId.Values
            .Where(p => p.IsSubmitted && !p.IsDeleted)
            .Select(p => new PendingParticipantUpdateSummaryEntity { ParticipantId = p.ParticipantId, PendingParticipantUpdateId = p.PendingParticipantUpdateId, LabCode = p.LabCode, LabName = p.Organisation })
            .ToList();
        return Task.FromResult(summaries);
    }

    public Task<PendingParticipantUpdate?> GetByParticipantIdAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byParticipantId.TryGetValue(participantId, out var pending) && pending.IsSubmitted && !pending.IsDeleted ? pending : null);

    public Task<bool> MarkDecidedAsync(Guid participantId, Guid pendingParticipantUpdateId, CancellationToken cancellationToken = default)
    {
        if (!_byParticipantId.TryGetValue(participantId, out var pending) || pending.PendingParticipantUpdateId != pendingParticipantUpdateId)
        {
            return Task.FromResult(false);
        }

        pending.IsDeleted = true;
        return Task.FromResult(true);
    }

    // Rows of tblUpdatedParticipantDetailsEmail that have no fldDateSent yet.
    public List<ParticipantUpdateEmailEntity> Unsent { get; } = [];
    public List<ParticipantUpdateNotification> QueuedNotifications { get; } = [];
    public Dictionary<Guid, DateTime> MarkedSent { get; } = [];
    public Exception? GetUnsentFailure { get; set; }
    public int GetUnsentCalls { get; private set; }

    public Task QueueNotificationAsync(ParticipantUpdateNotification notification, CancellationToken cancellationToken = default)
    {
        QueuedNotifications.Add(notification);
        Unsent.Add(new ParticipantUpdateEmailEntity
        {
            Id = Guid.NewGuid(),
            Email = notification.Email,
            LabCode = notification.LabCode,
            PendingParticipantUpdateId = notification.PendingParticipantUpdateId,
            ParticipantName = notification.ParticipantName,
            Notes = notification.Notes,
            IsApproved = notification.IsApproved,
            QalNumber = "QAL/00001"
        });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ParticipantUpdateEmailEntity>> GetUnsentNotificationsAsync(CancellationToken cancellationToken = default)
    {
        GetUnsentCalls++;
        return GetUnsentFailure is null
            ? Task.FromResult<IReadOnlyList<ParticipantUpdateEmailEntity>>([.. Unsent])
            : Task.FromException<IReadOnlyList<ParticipantUpdateEmailEntity>>(GetUnsentFailure);
    }

    public Task MarkNotificationSentAsync(Guid notificationId, DateTime sentAt, CancellationToken cancellationToken = default)
    {
        Unsent.RemoveAll(e => e.Id == notificationId);
        MarkedSent[notificationId] = sentAt;
        return Task.CompletedTask;
    }
}
