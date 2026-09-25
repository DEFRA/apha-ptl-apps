namespace PTL.Core.Participant;

// Field is the ParticipantFormViewModel/CreateParticipantRequest property name the error belongs to
// (empty string for a model-level error with no single field), so callers can render a GOV.UK
// error summary that links each message to its exact field.
public sealed record ParticipantValidationError(string Field, string Message);

public sealed record ParticipantValidationResult(bool IsValid, IReadOnlyList<ParticipantValidationError> Errors);
