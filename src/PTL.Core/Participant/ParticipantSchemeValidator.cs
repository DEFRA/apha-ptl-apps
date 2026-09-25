namespace PTL.Core.Participant;

public sealed record ParticipantSchemeValidationError(string Field, string Message);

public sealed class ParticipantSchemeValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<ParticipantSchemeValidationError> Errors { get; } = [];
}

public sealed class ParticipantSchemeValidationException(IReadOnlyList<ParticipantSchemeValidationError> errors) : Exception("Participant scheme validation failed.")
{
    public IReadOnlyList<ParticipantSchemeValidationError> Errors { get; } = errors;
}

// Matches the fields legacy ParticipantScheme.aspx actually validates before Save (a Scheme and
// Participant must be selected; text fields are bounded by their tlnkParticipantScheme column
// sizes - varchar(50)/varchar(100)/varchar(2000)). See docs/analysis/participant-analysis.md.
public static class ParticipantSchemeValidator
{
    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        ["SchemeId"] = "Scheme",
        ["ParticipantId"] = "Participant",
        ["ContractId"] = "Contract",
        ["NumberOfSetsRequired"] = "Number of sets required",
        ["ExternalReference"] = "External reference",
        ["Contact"] = "Contact",
        ["PackingInstructions"] = "Packing instructions"
    };

    public static ParticipantSchemeValidationResult Validate(ParticipantSchemeRecord record)
    {
        var result = new ParticipantSchemeValidationResult();

        RequireSelected(record.SchemeId, "SchemeId", result.Errors);
        RequireSelected(record.ParticipantId, "ParticipantId", result.Errors);
        RequireSelected(record.ContractId, "ContractId", result.Errors);

        if (record.NumberOfSetsRequired < 1)
        {
            result.Errors.Add(new ParticipantSchemeValidationError("NumberOfSetsRequired", $"{Label("NumberOfSetsRequired")} must be at least 1"));
        }

        MaxLength(record.ExternalReference, "ExternalReference", 50, result.Errors);
        MaxLength(record.Contact, "Contact", 100, result.Errors);
        MaxLength(record.PackingInstructions, "PackingInstructions", 2000, result.Errors);

        // Matches legacy AreDistributionMonthsValid(): ValidatorDistributionCustom.IsValid = False
        // when every CheckboxMonthX is unchecked - "Distribution must have at least one selected month".
        if (!HasAnyDistributionMonth(record))
        {
            result.Errors.Add(new ParticipantSchemeValidationError("DistributionMonthJan", "Distribution must have at least one selected month"));
        }

        return result;
    }

    private static bool HasAnyDistributionMonth(ParticipantSchemeRecord record) =>
        record.DistributionMonthJan || record.DistributionMonthFeb || record.DistributionMonthMar ||
        record.DistributionMonthApr || record.DistributionMonthMay || record.DistributionMonthJun ||
        record.DistributionMonthJul || record.DistributionMonthAug || record.DistributionMonthSep ||
        record.DistributionMonthOct || record.DistributionMonthNov || record.DistributionMonthDec;

    private static void RequireSelected(Guid value, string field, List<ParticipantSchemeValidationError> errors)
    {
        if (value == Guid.Empty)
        {
            errors.Add(new ParticipantSchemeValidationError(field, $"Select a {Label(field)}"));
        }
    }

    private static void MaxLength(string? value, string field, int maxLength, List<ParticipantSchemeValidationError> errors)
    {
        if (value is not null && value.Length > maxLength)
        {
            errors.Add(new ParticipantSchemeValidationError(field, $"{Label(field)} must not exceed {maxLength} characters"));
        }
    }

    private static string Label(string field) => FieldLabels.GetValueOrDefault(field, field);
}
