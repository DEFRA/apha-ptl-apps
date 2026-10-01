namespace PTL.Core.Participant;

public interface IParticipantService
{
    Task<Participant?> GetParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipantSummaryEntity>> GetParticipantsAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ParticipantSearchResult> SearchParticipantsAsync(Guid customerId, string? searchTerm, bool includeInactive, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Participant> CreateParticipantAsync(Participant participant, CancellationToken cancellationToken = default);
    Task<Participant?> UpdateParticipantAsync(Guid participantId, Participant updatedFields, CancellationToken cancellationToken = default);
    Task<Participant?> DeactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<Participant?> ReactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);

    // spgaPendingParticipantDetailsEditInfo - every outstanding pending participant update.
    Task<IReadOnlyList<PendingParticipantUpdateSummaryEntity>> GetPendingParticipantUpdatesAsync(CancellationToken cancellationToken = default);

    // Returns null when the participant or its pending update does not exist.
    Task<(Participant Current, PendingParticipantUpdate Pending)?> GetPendingParticipantUpdateAsync(Guid participantId, CancellationToken cancellationToken = default);

    // Applies the pending contact fields onto the live participant record, then soft-deletes the
    // pending update. When editedFields is supplied those values are applied in place of the stored
    // pending values (legacy ButtonApprove_Click writes the on-screen values to both records).
    // Returns false when no matching pending update exists. Throws ParticipantValidationException
    // when the resulting participant breaks business rules.
    Task<bool> ApprovePendingParticipantUpdateAsync(Guid participantId, PendingParticipantUpdate? editedFields = null, CancellationToken cancellationToken = default);

    // Soft-deletes the pending update without changing the live participant record.
    Task<bool> DeclinePendingParticipantUpdateAsync(Guid participantId, CancellationToken cancellationToken = default);
}
