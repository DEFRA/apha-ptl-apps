namespace PTL.Core.Participant;

// Defined in Core so ContractService can depend on the abstraction without Core referencing Data;
// PTL.Data.Participant.ParticipantSchemeRepository implements this. Deliberately scoped to only the
// operations Contract Items' removal workflow needs - not a general ParticipantScheme repository.
public interface IParticipantSchemeRepository
{
    // Uses spgParticipantSchemeByParticipantSchemeId.
    Task<ParticipantSchemeRecord?> GetByIdAsync(Guid participantSchemeId, CancellationToken cancellationToken = default);

    // Uses spiParticipantScheme; the returned record is re-read via GetByIdAsync so Price/
    // ParticipantDisplayName/SchemeDisplayName (all computed by the fetch procedure) are populated.
    Task<ParticipantSchemeRecord> CreateAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default);

    // Uses spuParticipantScheme; returns false when no row was updated (record does not exist).
    Task<bool> UpdateAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default);
}
