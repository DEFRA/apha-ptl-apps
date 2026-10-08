using System.Text.RegularExpressions;

namespace PTL.Core.Scheme;

// Preserves the validation rules identified in docs/analysis/scheme-analysis.md ("Validation
// Rules" section) verbatim - no additional rules invented beyond what is documented there.
public static partial class SchemeValidator
{
    // Field key shared by the Dictionary below and every validation call against Identifier.
    private const string IdentifierField = "Identifier";

    // Human-readable labels for error messages - error Field keys stay as the property name so
    // ModelState mapping and error-summary hrefs keep working.
    private static readonly Dictionary<string, string> FieldLabels = new(StringComparer.Ordinal)
    {
        [IdentifierField] = "Identifier",
        ["Name"] = "Scheme name",
        ["Deadline"] = "Deadline",
        ["SampleOrigin"] = "Sample origin",
        ["NumberOfSamples"] = "Number of samples",
        ["Instructions"] = "Scheme instructions",
        ["CustomsDescription"] = "Customs description",
        ["CustomsVolume"] = "Sample volume",
        ["Subcontractor"] = "Subcontractor",
        ["SamplePackingInstructions"] = "Sample packing instructions",
        ["StandardTabulationText"] = "Standard tabulation text",
        ["DataConsentDeclarationText"] = "Consent text",
    };

    private static string Label(string field) => FieldLabels.TryGetValue(field, out var label) ? label : field;

    [GeneratedRegex("^PT[0-9]{4}$", RegexOptions.None, 1000)]
    private static partial Regex IdentifierPattern();

    public static SchemeValidationResult Validate(Scheme scheme)
    {
        var errors = new List<SchemeValidationError>();

        RequireNotEmpty(scheme.Identifier, IdentifierField, errors);
        MaxLength(scheme.Identifier, 6, IdentifierField, errors);
        if (!string.IsNullOrEmpty(scheme.Identifier) && !IdentifierPattern().IsMatch(scheme.Identifier))
        {
            errors.Add(new SchemeValidationError(IdentifierField, "Identifier must match the format PT followed by 4 digits (e.g. PT1234)"));
        }

        RequireNotEmpty(scheme.Name, "Name", errors);
        MaxLength(scheme.Name, 100, "Name", errors);

        Range(scheme.Deadline, 1, 999, "Deadline", errors);

        RequireNotEmpty(scheme.SampleOrigin, "SampleOrigin", errors);
        MaxLength(scheme.SampleOrigin, 50, "SampleOrigin", errors);

        Range(scheme.NumberOfSamples, 1, 999, "NumberOfSamples", errors);

        // Legacy validated TBEdit through its ValidationProperty("TextPlain"), so markup that
        // renders as nothing - an empty paragraph, say - never satisfied the required rule.
        RequireNotEmpty(SchemeInstructions.ToPlainText(scheme.Instructions), "Instructions", errors);
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
            errors.Add(new SchemeValidationError("DistributionAsAvailable", "Distribution must have at least one selected month or be marked as as available"));
        }

        // ValidateDataConsentDeclaration: DataConsentDeclarationText is required only if
        // DataConsentDeclarationActive is true.
        if (scheme.DataConsentDeclarationActive && string.IsNullOrWhiteSpace(scheme.DataConsentDeclarationText))
        {
            errors.Add(new SchemeValidationError("DataConsentDeclarationText", "Enter the consent text when the Data Consent Declaration is active"));
        }

        ValidateAssessmentStaffing(scheme, errors);
        ValidateTabulations(scheme, errors);

        return new SchemeValidationResult(errors.Count == 0, errors);
    }

    // Legacy ButtonSave_Click: a non-assessment scheme needs one tabulation for the Test
    // Consultant and one that can be published; an assessment scheme needs only one. The
    // Test Consultant tabulation selector is hidden for assessment schemes, so it is only
    // required when assessment is not.
    private static void ValidateTabulations(Scheme scheme, List<SchemeValidationError> errors)
    {
        if (scheme.RequiresAssessment)
        {
            if (scheme.Tabulations.Count < 1)
            {
                errors.Add(new SchemeValidationError("Tabulations", "You must define at least one Tabulation"));
            }

            return;
        }

        if (scheme.Tabulations.Count < 2)
        {
            errors.Add(new SchemeValidationError("Tabulations", "You must define at least two Tabulations - one for the Test Consultant and one that can be Published"));
            return;
        }

        // Both failure cases (nothing selected, or a selection that doesn't match any tabulation)
        // report the same message against the same field, so they are one condition rather than
        // two branches with identical bodies.
        var selectionIsValid = scheme.TestConsultantTabulationId is { } selected && selected != Guid.Empty && scheme.Tabulations.Any(t => t.TabulationId == selected);
        if (!selectionIsValid)
        {
            errors.Add(new SchemeValidationError("TestConsultantTabulationId", "A Tabulation must be selected to send to the test consultants"));
        }
    }

    // Requires Assessment decides which of the two tabs applies: assessment schemes need at least
    // a Primary and a Secondary Assessor, non-assessment schemes need a Primary Test Consultant.
    // Neither list may name the same person twice.
    private static void ValidateAssessmentStaffing(Scheme scheme, List<SchemeValidationError> errors)
    {
        if (scheme.RequiresAssessment)
        {
            RequireSelected(scheme.Assessor1, "Assessor1", "Select a Primary Assessor", errors);
            RequireSelected(scheme.Assessor2, "Assessor2", "Select a Secondary Assessor", errors);
            RequireDistinct(
                [("Assessor1", scheme.Assessor1), ("Assessor2", scheme.Assessor2), ("Assessor3", scheme.Assessor3), ("Assessor4", scheme.Assessor4)],
                "The same Assessor cannot be selected more than once",
                errors);
            return;
        }

        RequireSelected(scheme.TestConsultant1, "TestConsultant1", "Select a Primary Test Consultant", errors);
        RequireDistinct(
            [("TestConsultant1", scheme.TestConsultant1), ("TestConsultant2", scheme.TestConsultant2), ("TestConsultant3", scheme.TestConsultant3)],
            "The same Test Consultant cannot be selected more than once",
            errors);
    }

    private static void RequireSelected(Guid? value, string field, string message, List<SchemeValidationError> errors)
    {
        if (value is null || value == Guid.Empty)
        {
            errors.Add(new SchemeValidationError(field, message));
        }
    }

    private static void RequireDistinct(IReadOnlyList<(string Field, Guid? Value)> selections, string message, List<SchemeValidationError> errors)
    {
        var seen = new HashSet<Guid>();
        foreach (var (field, value) in selections)
        {
            if (value is not { } id || id == Guid.Empty)
            {
                continue;
            }

            if (!seen.Add(id))
            {
                errors.Add(new SchemeValidationError(field, message));
            }
        }
    }

    private static void RequireNotEmpty(string? value, string field, List<SchemeValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new SchemeValidationError(field, $"{Label(field)} is required"));
        }
    }

    private static void MaxLength(string? value, int max, string field, List<SchemeValidationError> errors)
    {
        if (value is not null && value.Length > max)
        {
            errors.Add(new SchemeValidationError(field, $"{Label(field)} must not exceed {max} characters"));
        }
    }

    private static void Range(int value, int min, int max, string field, List<SchemeValidationError> errors)
    {
        if (value < min || value > max)
        {
            errors.Add(new SchemeValidationError(field, $"{Label(field)} must be between {min} and {max}"));
        }
    }
}
