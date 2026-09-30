namespace PTL.Core.Participant;

// Defined in Core (not Data) so ParticipantService can depend on the abstraction without Core
// referencing Data; PTL.Data.Participant.PendingParticipantUpdateRepository implements this.
public interface IPendingParticipantUpdateRepository
{
    // spgaPendingParticipantDetailsEditInfo - every outstanding (submitted, not-deleted) update.
    Task<IReadOnlyList<PendingParticipantUpdateSummaryEntity>> GetSummariesAsync(CancellationToken cancellationToken = default);

    // spgPendingParticipantDetailsEditByParticipantID @IsSubmitted=1 - the single outstanding
    // pending update for a participant, or null if there is none.
    Task<PendingParticipantUpdate?> GetByParticipantIdAsync(Guid participantId, CancellationToken cancellationToken = default);

    // spdPendingParticipantDetailsEditByParticipantID - soft-deletes the pending record (sets
    // fldIsDeleted=1); used on both Approve and Decline, since neither path reinstates the row.
    // Returns false when the row is still outstanding afterwards.
    Task<bool> MarkDecidedAsync(Guid participantId, Guid pendingParticipantUpdateId, CancellationToken cancellationToken = default);
}
