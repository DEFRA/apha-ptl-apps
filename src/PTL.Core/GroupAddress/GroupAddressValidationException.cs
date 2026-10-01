namespace PTL.Core.GroupAddress;

public sealed class GroupAddressValidationException(IReadOnlyList<GroupAddressValidationError> errors)
    : Exception("Group Address validation failed.")
{
    public IReadOnlyList<GroupAddressValidationError> Errors { get; } = errors;
}

public sealed record GroupAddressValidationError(string Field, string Message);
