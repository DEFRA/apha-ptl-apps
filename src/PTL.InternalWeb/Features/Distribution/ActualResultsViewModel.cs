using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.Distribution;

// Actual Results screen - legacy Internal User/ActualResults.aspx. Reached from the Results Receipt
// screen's per-participant Results "View/Edit" link.
public sealed class ActualResultsViewModel : IValidatableObject
{
    // S6964 suppressed across the hidden/round-tripped inputs: they are posted back verbatim so the
    // form can be redisplayed after a validation failure without losing the user's edits.
#pragma warning disable S6964
    public int YearId { get; set; }

    public int MonthId { get; set; }

    public Guid MonthlyDistributionSchemeId { get; set; }

    public Guid DistributionParticipantId { get; set; }

    // Whether THIS redisplay is the audit-reason step (legacy Audit.aspx), shown only when
    // results already exist and a save is attempted.
    public bool IsAuditStep { get; set; }
#pragma warning restore S6964

    // Legacy LblDistribution: "<Distribution> <SchemeIdentifier>: <SchemeName>". Display-only,
    // re-read from the API on every redisplay rather than posted.
    public string? Heading { get; set; }

    // Legacy ActualResultsSet.AuditObjectName(), shown as the heading on the audit-reason step.
    public string? AuditObjectName { get; set; }

    // Legacy "Save Failed: No results can be saved after the deadline date." panel.
    public bool SaveFailed { get; set; }

    // Deadline has passed: the page shows the results read-only. Re-read from the API, never posted.
    public bool IsReadOnly { get; set; }

    // Which button the user pressed before the audit-reason step (save or apply), so the audited
    // save can land where legacy's Audit.aspx AuditLeavePage would.
    public string? PendingCommand { get; set; }

    [StringLength(2000, ErrorMessage = "Input can not exceed 2000 characters")]
    [Display(Name = "Reason for these changes")]
    public string? AuditWhy { get; set; }

    public List<ActualResultsTestViewModel> Tests { get; set; } = [];

    // Legacy Audit.aspx ValidatorAuditWhyRequired - only in force once the audit step is showing.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsAuditStep && string.IsNullOrWhiteSpace(AuditWhy))
        {
            yield return new ValidationResult("The reason for these changes must be specified", [nameof(AuditWhy)]);
        }
    }
}

// One table per Test: Result Items as columns, applicable Samples as rows, Method Items as rows.
public sealed class ActualResultsTestViewModel
{
    public Guid TestId { get; set; }

    public string? TestName { get; set; }

    public List<ActualResultsResultItemViewModel> ResultItems { get; set; } = [];

    public List<ActualResultsMethodItemViewModel> MethodItems { get; set; } = [];

    public List<ActualResultsSampleRowViewModel> Samples { get; set; } = [];

    // Index of the sample's value for this result item, or -1 when absent.
    public int ValueIndexFor(int sampleIndex, Guid testResultItemId) =>
        Samples[sampleIndex].Values.FindIndex(v => v.TestResultItemId == testResultItemId);
}

// A FormatString starting with "^" is a regex (free-text box); otherwise it is a "|"-delimited list
// of dropdown options - legacy BuildResultCellContents' exact rule.
public sealed class ActualResultsResultItemViewModel
{
    public Guid TestResultItemId { get; set; }

    public string? Name { get; set; }

    public string? FormatString { get; set; }

    public string? ReturnValueType { get; set; }

    public bool IsDropdown => !(FormatString ?? string.Empty).StartsWith('^');

    public IReadOnlyList<string> DropdownOptions =>
        IsDropdown ? (FormatString ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries) : [];

    // Legacy tooltip / validator text: "Input must be of format: <ReturnValueType>".
    public string InputFormatMessage => $"Input must be of format: {ReturnValueType}";
}

public sealed class ActualResultsMethodItemViewModel
{
    public Guid TestMethodItemId { get; set; }

    public string? Name { get; set; }

    public string? FormatString { get; set; }

    public string? ReturnValueType { get; set; }

    public string? Result { get; set; }

    public bool IsDropdown => !(FormatString ?? string.Empty).StartsWith('^');

    public IReadOnlyList<string> DropdownOptions =>
        IsDropdown ? (FormatString ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries) : [];

    public string InputFormatMessage => $"Input must be of format: {ReturnValueType}";
}

public sealed class ActualResultsSampleRowViewModel
{
    public Guid SampleId { get; set; }

    public string? SampleNumber { get; set; }

    public List<ActualResultsSampleValueViewModel> Values { get; set; } = [];
}

public sealed class ActualResultsSampleValueViewModel
{
    public Guid SampleResultItemId { get; set; }

    public Guid TestResultItemId { get; set; }

    public string? Result { get; set; }
}
