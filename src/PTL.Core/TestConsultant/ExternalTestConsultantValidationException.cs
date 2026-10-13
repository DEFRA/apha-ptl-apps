namespace PTL.Core.TestConsultant;

public sealed class ExternalTestConsultantValidationException(IReadOnlyList<ExternalTestConsultantValidationError> errors)
    : Exception("External Test Consultant validation failed.")
{
    public IReadOnlyList<ExternalTestConsultantValidationError> Errors { get; } = errors;
}
