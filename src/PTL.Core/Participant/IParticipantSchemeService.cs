namespace PTL.Core.Participant;

public interface IParticipantSchemeService
{
    Task<ParticipantSchemeRecord?> GetParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default);

    // Throws ParticipantSchemeValidationException on invalid input or when the owning contract IsReadOnly.
    Task<ParticipantSchemeRecord> CreateParticipantSchemeAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default);

    // Returns null when participantSchemeId does not exist. Throws ParticipantSchemeValidationException
    // on invalid input or when the owning contract IsReadOnly.
    Task<ParticipantSchemeRecord?> UpdateParticipantSchemeAsync(Guid participantSchemeId, ParticipantSchemeRecord updatedFields, CancellationToken cancellationToken = default);

    // Soft-delete (fldIsRemoved = true), matching legacy's BtnSave_Click deferred-delete Save flow
    // made explicit - see docs/migration/contract-migration.md. Returns false when not found.
    Task<bool> DeleteParticipantSchemeAsync(Guid participantSchemeId, CancellationToken cancellationToken = default);
}
