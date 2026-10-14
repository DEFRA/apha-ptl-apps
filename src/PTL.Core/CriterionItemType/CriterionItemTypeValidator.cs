namespace PTL.Core.CriterionItemType;

// Required-field + max-length validation only - matches legacy ValidatorNewNameRequired/
// ValidatorNameRequired (both read "Name required"). MaxNameLength resolves a previously
// [UNVERIFIED] parity-doc question: the legacy new-row textbox is MaxLength=50 but the edit-row
// textbox is MaxLength=100 (a legacy markup inconsistency); spiCriterionItemType.sql/
// spuCriterionItemType.sql both declare @Name varchar(50), confirming 50 is the true DB
// constraint and the edit-row's 100 was the bug, not the new-row's 50.
public static class CriterionItemTypeValidator
{
    public const int MaxNameLength = 50;

    public static CriterionItemTypeValidationResult Validate(string name)
    {
        var errors = new List<CriterionItemTypeValidationError>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new CriterionItemTypeValidationError("Name", "Name required"));
        }
        else if (name.Length > MaxNameLength)
        {
            errors.Add(new CriterionItemTypeValidationError("Name", $"Name must be {MaxNameLength} characters or fewer"));
        }

        return new CriterionItemTypeValidationResult(errors.Count == 0, errors);
    }
}

public sealed record CriterionItemTypeValidationError(string Field, string Message);
public sealed record CriterionItemTypeValidationResult(bool IsValid, IReadOnlyList<CriterionItemTypeValidationError> Errors);

public sealed class CriterionItemTypeValidationException(IReadOnlyList<CriterionItemTypeValidationError> errors) : Exception
{
    public IReadOnlyList<CriterionItemTypeValidationError> Errors { get; } = errors;
}

// Thrown when editing an item still referenced by a Scheme/Distribution - legacy only blocks this
// client-side (OnClientClick alert); this is the server-side equivalent.
public sealed class CriterionItemTypeInUseException(string message) : Exception(message);
