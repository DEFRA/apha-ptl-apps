namespace PTL.Core.TestType;

// Required-field + max-length validation only - matches legacy ValidatorNewNameRequired/
// ValidatorNameRequired (both read "Name required"). 30 characters is the shortest Name limit of
// the 5 Scheme Admin item-type screens (TestTypeList.aspx TextboxNewName/TextboxName MaxLength).
public static class TestTypeValidator
{
    public const int MaxNameLength = 30;

    public static TestTypeValidationResult Validate(string name)
    {
        var errors = new List<TestTypeValidationError>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new TestTypeValidationError("Name", "Name required"));
        }
        else if (name.Length > MaxNameLength)
        {
            errors.Add(new TestTypeValidationError("Name", $"Name must be {MaxNameLength} characters or fewer"));
        }

        return new TestTypeValidationResult(errors.Count == 0, errors);
    }
}

public sealed record TestTypeValidationError(string Field, string Message);
public sealed record TestTypeValidationResult(bool IsValid, IReadOnlyList<TestTypeValidationError> Errors);

public sealed class TestTypeValidationException(IReadOnlyList<TestTypeValidationError> errors) : Exception
{
    public IReadOnlyList<TestTypeValidationError> Errors { get; } = errors;
}

// Thrown when editing an item still referenced by a Scheme/Distribution - legacy only blocks this
// client-side (OnClientClick alert); this is the server-side equivalent.
public sealed class TestTypeInUseException(string message) : Exception(message);
