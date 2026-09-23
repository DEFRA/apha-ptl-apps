using System.Text.RegularExpressions;

namespace PTL.Core.Scheme;

// Preserves the validation rules identified in docs/analysis/scheme-analysis.md ("Validation
// Rules" section) verbatim - no additional rules invented beyond what is documented there.
public static partial class SchemeValidator
{
    [GeneratedRegex("^PT[0-9]{4}$", RegexOptions.None, 1000)]
    private static partial Regex IdentifierPattern();

    public static SchemeValidationResult Validate(Scheme scheme)
    {
        var errors = new List<SchemeValidationError>();

        RequireNotEmpty(scheme.Identifier, "Identifier", errors);
        MaxLength(scheme.Identifier, 6, "Identifier", errors);
        if (!string.IsNullOrEmpty(scheme.Identifier) && !IdentifierPattern().IsMatch(scheme.Identifier))
        {
            errors.Add(new SchemeValidationError("Identifier", "Identifier must match the format PT followed by 4 digits (e.g. PT1234)"));
        }

        RequireNotEmpty(scheme.Name, "Name", errors);
        MaxLength(scheme.Name, 100, "Name", errors);

        Range(scheme.Deadline, 1, 999, "Deadline", errors);

        RequireNotEmpty(scheme.SampleOrigin, "SampleOrigin", errors);
        MaxLength(scheme.SampleOrigin, 50, "SampleOrigin", errors);

        Range(scheme.NumberOfSamples, 1, 999, "NumberOfSamples", errors);

        RequireNotEmpty(scheme.Instructions, "Instructions", errors);
        MaxLength(scheme.Instructions, 50000, "Instructions", errors);

        RequireNotEmpty(scheme.CustomsDescription, "CustomsDescription", errors);
        MaxLength(scheme.CustomsDescription, 500, "CustomsDescription", errors);

        RequireNotEmpty(scheme.CustomsVolume, "CustomsVolume", errors);
        MaxLength(scheme.CustomsVolume, 20, "CustomsVolume", errors);

        MaxLength(scheme.Subcontractor, 50, "Subcontractor", errors);
        MaxLength(scheme.SamplePackingInstructions, 2000, "SamplePackingInstructions", errors);
        MaxLength(scheme.StandardTabulationText, 500, "StandardTabulationText", errors);
        MaxLength(scheme.DataConsentDeclarationText, 500, "DataConsentDeclarationText", errors);

        // ValidateDistribution: distribution months and DistributionAsAvailable are mutually
        // exclusive - a scheme must have either specific months set or be "as available", never
        // both and never neither.
        var hasAnyMonth = scheme.DistributionMonthJan || scheme.DistributionMonthFeb || scheme.DistributionMonthMar
            || scheme.DistributionMonthApr || scheme.DistributionMonthMay || scheme.DistributionMonthJun
            || scheme.DistributionMonthJul || scheme.DistributionMonthAug || scheme.DistributionMonthSep
            || scheme.DistributionMonthOct || scheme.DistributionMonthNov || scheme.DistributionMonthDec;
        if (hasAnyMonth == scheme.DistributionAsAvailable)
        {
            errors.Add(new SchemeValidationError("DistributionAsAvailable", "Select either specific distribution months or 'as available', but not both"));
        }

        // ValidateDataConsentDeclaration: DataConsentDeclarationText is required only if
        // DataConsentDeclarationActive is true.
        if (scheme.DataConsentDeclarationActive && string.IsNullOrWhiteSpace(scheme.DataConsentDeclarationText))
        {
            errors.Add(new SchemeValidationError("DataConsentDeclarationText", "Enter the consent text when the Data Consent Declaration is active"));
        }

        return new SchemeValidationResult(errors.Count == 0, errors);
    }

    private static void RequireNotEmpty(string? value, string field, List<SchemeValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new SchemeValidationError(field, $"{field} is required"));
        }
    }

    private static void MaxLength(string? value, int max, string field, List<SchemeValidationError> errors)
    {
        if (value is not null && value.Length > max)
        {
            errors.Add(new SchemeValidationError(field, $"{field} must not exceed {max} characters"));
        }
    }

    private static void Range(int value, int min, int max, string field, List<SchemeValidationError> errors)
    {
        if (value < min || value > max)
        {
            errors.Add(new SchemeValidationError(field, $"{field} must be between {min} and {max}"));
        }
    }
}
