namespace PTL.Core.Distribution;

public sealed record SampleNumbersError(string Field, string Message);

// Ports the legacy CSLA AddBusinessRules on IntendedResults.Sample and IntendedResultsSet, plus
// the two RegularExpressionValidators on SampleNumbers.aspx, VERBATIM. Only Sample Number and
// Internal Description are required - legacy has a RequiredFieldValidator on exactly those two
// and nothing else on this screen, so no other field may be made mandatory. In particular
// Comments, Test Instruction, Tabulation Description and Description for TC are all OPTIONAL.
// Message provenance: the two required messages use GOV.UK "Enter the ..." wording in place of
// the legacy tooltip text. "Test Instruction must not exceed 500 characters" and "Description
// for TC must not exceed 500 characters" are legacy text. The remaining max-length messages are
// new wording for rules legacy enforced silently (a MaxLength attribute that truncated, or a
// CSLA rule that threw) - the rules themselves are legacy, only the user-facing text is new.
// PTL.InternalWeb repeats the required/max-length rules as DataAnnotations so the user gets
// GOV.UK field-level errors without a round trip; this validator is the API's own defence and
// must not be removed on that basis.
public static class SampleNumbersValidator
{
    public const string SampleNumber = "SampleNumber";
    public const string InternalDescription = "InternalDescription";
    public const string ExternalDescription = "ExternalDescription";
    public const string TabulationDescription = "TabulationDescription";
    public const string TestConsultantsDescription = "TestConsultantsDescription";
    public const string Comments = "Comments";

    // tblSample column widths, mirrored by the legacy CSLA MaxLengthRuleArgs.
    public const int SampleNumberMaxLength = 50;
    public const int InternalDescriptionMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int CommentsMaxLength = 8000;

    public static IReadOnlyList<SampleNumbersError> ValidateComments(string? comments)
    {
        var errors = new List<SampleNumbersError>();
        if ((comments ?? string.Empty).Length > CommentsMaxLength)
        {
            errors.Add(new SampleNumbersError(Comments, $"Comments must not exceed {CommentsMaxLength} characters"));
        }

        return errors;
    }

    public static IReadOnlyList<SampleNumbersError> ValidateSample(SampleNumberRowUpdate sample)
    {
        var errors = new List<SampleNumbersError>();

        AddRequired(errors, SampleNumber, sample.SampleNumber, "Enter the Sample Number");
        AddMaxLength(errors, SampleNumber, sample.SampleNumber, SampleNumberMaxLength, $"Sample Number must not exceed {SampleNumberMaxLength} characters");
        AddRequired(errors, InternalDescription, sample.InternalDescription, "Enter the Internal Description");
        AddMaxLength(errors, InternalDescription, sample.InternalDescription, InternalDescriptionMaxLength, $"Internal Description must not exceed {InternalDescriptionMaxLength} characters");
        AddMaxLength(errors, ExternalDescription, sample.ExternalDescription, DescriptionMaxLength, "Test Instruction must not exceed 500 characters");
        AddMaxLength(errors, TabulationDescription, sample.TabulationDescription, DescriptionMaxLength, "Tabulation Description must not exceed 500 characters");
        AddMaxLength(errors, TestConsultantsDescription, sample.TestConsultantsDescription, DescriptionMaxLength, "Description for TC must not exceed 500 characters");

        return errors;
    }

    private static void AddRequired(List<SampleNumbersError> errors, string field, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new SampleNumbersError(field, message));
        }
    }

    private static void AddMaxLength(List<SampleNumbersError> errors, string field, string? value, int maxLength, string message)
    {
        if ((value ?? string.Empty).Length > maxLength)
        {
            errors.Add(new SampleNumbersError(field, message));
        }
    }
}
