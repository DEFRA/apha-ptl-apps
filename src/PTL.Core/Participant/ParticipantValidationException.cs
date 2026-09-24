namespace PTL.Core.Participant;

public sealed class ParticipantValidationException(IReadOnlyList<ParticipantValidationError> errors) : Exception("Participant validation failed.")
{
    public IReadOnlyList<ParticipantValidationError> Errors { get; } = errors;
}
