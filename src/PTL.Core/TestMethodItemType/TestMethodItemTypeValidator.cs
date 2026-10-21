namespace PTL.Core.TestMethodItemType;

// Required-field + max-length validation on Name, plus a required check on the Return Type -
// matches legacy ValidatorNewNameRequired/ValidatorNameRequired (both read "Name required",
// MaxLength 100) and ValidatorNewReturnValueRequired/ValidatorReturnValueRequired (RequiredField
// with InitialValue = Guid.Empty, matching the "- Please Select -" placeholder option).
public static class TestMethodItemTypeValidator
{
    public const int MaxNameLength = 100;

    public static TestMethodItemTypeValidationResult Validate(string name, Guid testReturnValueTypeId)
    {
        var errors = new List<TestMethodItemTypeValidationError>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new TestMethodItemTypeValidationError("Name", "Name required"));
        }
        else if (name.Length > MaxNameLength)
        {
            errors.Add(new TestMethodItemTypeValidationError("Name", $"Name must be {MaxNameLength} characters or fewer"));
        }

        if (testReturnValueTypeId == Guid.Empty)
        {
            errors.Add(new TestMethodItemTypeValidationError("TestReturnValueTypeId", "Return Type required"));
        }

        return new TestMethodItemTypeValidationResult(errors.Count == 0, errors);
    }
}

public sealed record TestMethodItemTypeValidationError(string Field, string Message);
public sealed record TestMethodItemTypeValidationResult(bool IsValid, IReadOnlyList<TestMethodItemTypeValidationError> Errors);

public sealed class TestMethodItemTypeValidationException(IReadOnlyList<TestMethodItemTypeValidationError> errors) : Exception
{
    public IReadOnlyList<TestMethodItemTypeValidationError> Errors { get; } = errors;
}

// Thrown when editing an item still referenced by a Scheme/Distribution - legacy only blocks this
// client-side (OnClientClick alert); this is the server-side equivalent.
public sealed class TestMethodItemTypeInUseException(string message) : Exception(message);
