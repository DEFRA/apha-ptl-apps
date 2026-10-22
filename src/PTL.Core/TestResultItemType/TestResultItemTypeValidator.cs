namespace PTL.Core.TestResultItemType;

// Required-field + max-length validation on Name, plus a required check on the Return Type -
// matches legacy ValidatorNewNameRequired/ValidatorNameRequired (both read "Name required",
// MaxLength 100) and ValidatorNewReturnValueRequired/ValidatorReturnValueRequired (RequiredField
// with InitialValue = Guid.Empty, matching the "- Please Select -" placeholder option).
public static class TestResultItemTypeValidator
{
    public const int MaxNameLength = 100;

    public static TestResultItemTypeValidationResult Validate(string name, Guid testReturnValueTypeId)
    {
        var errors = new List<TestResultItemTypeValidationError>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new TestResultItemTypeValidationError("Name", "Name required"));
        }
        else if (name.Length > MaxNameLength)
        {
            errors.Add(new TestResultItemTypeValidationError("Name", $"Name must be {MaxNameLength} characters or fewer"));
        }

        if (testReturnValueTypeId == Guid.Empty)
        {
            errors.Add(new TestResultItemTypeValidationError("TestReturnValueTypeId", "Return Type required"));
        }

        return new TestResultItemTypeValidationResult(errors.Count == 0, errors);
    }
}

public sealed record TestResultItemTypeValidationError(string Field, string Message);
public sealed record TestResultItemTypeValidationResult(bool IsValid, IReadOnlyList<TestResultItemTypeValidationError> Errors);

public sealed class TestResultItemTypeValidationException(IReadOnlyList<TestResultItemTypeValidationError> errors) : Exception
{
    public IReadOnlyList<TestResultItemTypeValidationError> Errors { get; } = errors;
}

// Thrown when editing an item still referenced by a Scheme/Distribution - legacy only blocks this
// client-side (OnClientClick alert); this is the server-side equivalent.
public sealed class TestResultItemTypeInUseException(string message) : Exception(message);
