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

    // Legacy ContractItems removal (SetAudit + Delete + Save): the distribution sync, then
    // spiAuditParticipantScheme, then spuParticipantScheme - in that order, in one transaction.
    Task<bool> RemoveAsync(ParticipantSchemeRecord record, ParticipantSchemeAudit audit, CancellationToken cancellationToken = default);

    // Uses spgSchemeBySchemeId (fldCanEditXParticipants, or ...Overseas when nonUk) and
    // spgParticipantSchemeWarning - legacy ParticipantScheme.SchemeId's setter.
    Task<ParticipantSchemeContext> GetSchemeContextAsync(Guid schemeId, Guid contractId, bool nonUk, CancellationToken cancellationToken = default);

    // Uses spgParticipantSchemeInfoByParticipantIdAndSchemeId - every contract item (any contract,
    // removed ones included) the participant already has on the scheme.
    Task<IReadOnlyList<OtherParticipantScheme>> GetByParticipantAndSchemeAsync(Guid participantId, Guid schemeId, CancellationToken cancellationToken = default);
}
