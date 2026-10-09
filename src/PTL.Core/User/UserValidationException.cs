namespace PTL.Core.User;

public sealed class UserValidationException(IReadOnlyList<UserValidationError> errors)
    : Exception("User validation failed.")
{
    public IReadOnlyList<UserValidationError> Errors { get; } = errors;
}
