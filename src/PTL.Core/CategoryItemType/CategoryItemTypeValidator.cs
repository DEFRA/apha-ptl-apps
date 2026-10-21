namespace PTL.Core.CategoryItemType;

// Required-field validation only - matches legacy ValidatorNewCategoryNameRequired/
// ValidatorNameRequired (both read "Name required").
public static class CategoryItemTypeValidator
{
    public const int MaxNameLength = 50;

    public static CategoryItemTypeValidationResult Validate(string name)
    {
        var errors = new List<CategoryItemTypeValidationError>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new CategoryItemTypeValidationError("Name", "Name required"));
        }
        else if (name.Length > MaxNameLength)
        {
            errors.Add(new CategoryItemTypeValidationError("Name", $"Name must be {MaxNameLength} characters or fewer"));
        }

        return new CategoryItemTypeValidationResult(errors.Count == 0, errors);
    }
}

public sealed record CategoryItemTypeValidationError(string Field, string Message);
public sealed record CategoryItemTypeValidationResult(bool IsValid, IReadOnlyList<CategoryItemTypeValidationError> Errors);

public sealed class CategoryItemTypeValidationException(IReadOnlyList<CategoryItemTypeValidationError> errors) : Exception
{
    public IReadOnlyList<CategoryItemTypeValidationError> Errors { get; } = errors;
}

// Thrown when editing an item still referenced by a Scheme/Distribution - legacy only blocks this
// client-side (OnClientClick alert); this is the server-side equivalent, matching
// CountryService.DeleteAsync's precedent of enforcing usage guards in the service layer.
public sealed class CategoryItemTypeInUseException(string message) : Exception(message);
