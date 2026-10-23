namespace PTL.Core.User;

public sealed class UserValidationException(IReadOnlyList<UserValidationError> errors, Exception? innerException = null)
    : Exception("User validation failed.", innerException)
{
    public IReadOnlyList<UserValidationError> Errors { get; } = errors;
}
