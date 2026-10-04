namespace PTL.Contracts.Participant;

// Mirrors PTL.Contracts.Contract.ContractSaveResult - ApiClient-only wrapper distinguishing a
// successful save from field-level validation errors, without an unhandled HTTP exception.
public sealed record ParticipantSchemeSaveResult(bool Success, ParticipantSchemeResponse? ParticipantScheme, IReadOnlyDictionary<string, string[]> FieldErrors);
