namespace PTL.Core.ExternalSiteMessage;

public sealed class ExternalSiteMessageValidationException(IReadOnlyList<ExternalSiteMessageValidationError> errors)
    : Exception("External site message validation failed.")
{
    public IReadOnlyList<ExternalSiteMessageValidationError> Errors { get; } = errors;
}
