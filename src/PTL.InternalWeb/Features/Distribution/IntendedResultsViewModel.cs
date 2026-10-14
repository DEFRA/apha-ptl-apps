using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.Distribution;

// Intended Results screen - legacy Scheduling/IntendedResults.aspx. Reached from the Monthly
// Distributions (Scheduling) screen's "View Intended Results" link, only once Sample Numbers has
// been defined (legacy silently redirects back to Scheduling otherwise).
public sealed class IntendedResultsViewModel : IValidatableObject
{
    // S6964 suppressed across the hidden/round-tripped inputs: they are posted back verbatim so
    // the form can be redisplayed after a validation failure without losing the user's edits.
#pragma warning disable S6964
    public Guid MonthlyDistributionSchemeId { get; set; }

    public int YearId { get; set; }

    public int MonthId { get; set; }
#pragma warning restore S6964

    // Legacy LblDistribution: reference + " " + identifier + ": " + name. Nullable deliberately -
    // these are display-only, re-read from the API on every redisplay rather than posted, so a
    // non-nullable string would trip ASP.NET Core's implicit-required rule for reference types.
    public string? DistributionReferenceFull { get; set; }

    public string? SchemeIdentifier { get; set; }

    public string? SchemeName { get; set; }

    public string DistributionHeading => $"{DistributionReferenceFull} {SchemeIdentifier}: {SchemeName}";

    // Legacy IntendedResultsSet.RequireAudit: true the moment ANY intended result anywhere in the
    // distribution was already non-empty when fetched - computed once at GET time and round-
    // tripped unchanged through the whole edit session, never recomputed from what is posted.
#pragma warning disable S6964
    public bool RequiresAuditReason { get; set; }
#pragma warning restore S6964

    // Legacy IntendedResultsSet.AuditObjectName(), shown as the heading on the audit-reason step.
    public string? AuditObjectName { get; set; }

    // Whether THIS redisplay of the form is the audit-reason step - distinct from
    // RequiresAuditReason, which can already be true straight off a GET (the gate is a property
    // of the stored data, not of this submission) long before the user ever reaches that step.
    // Only the controller's audit-required branch sets this true; it is otherwise round-tripped
    // unchanged so a failed resubmit of the audit step redisplays the audit step, not the form.
#pragma warning disable S6964
    public bool IsAuditStep { get; set; }
#pragma warning restore S6964

    // Legacy Audit.aspx always returns to ~/Scheduling/monthlys.aspx for this screen (there is no
    // second destination, unlike Sample Numbers' "Save and go to Intended Results") - carried
    // through only so the audit step's Cancel can redisplay with the same model shape.
    public string? PendingCommand { get; set; }

    [StringLength(2000, ErrorMessage = "Input can not exceed 2000 characters")]
    [Display(Name = "Reason for these changes")]
    public string? AuditWhy { get; set; }

    public List<IntendedResultsTestViewModel> Tests { get; set; } = [];

    // Legacy Audit.aspx ValidatorAuditWhyRequired - only in force once the audit step is showing.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsAuditStep && string.IsNullOrWhiteSpace(AuditWhy))
        {
            yield return new ValidationResult(
                "The reason for these changes must be specified", [nameof(AuditWhy)]);
        }
    }
}

// One vertically-stacked table - legacy builds one per Test, Result Items as columns, Samples as
// rows, Method Items as test-level rows shared across every sample below the sample rows.
public sealed class IntendedResultsTestViewModel
{
    public Guid TestId { get; set; }

    public string? TestName { get; set; }

    public List<IntendedResultsResultItemViewModel> ResultItems { get; set; } = [];

    public List<IntendedResultsMethodItemViewModel> MethodItems { get; set; } = [];

    public List<IntendedResultsSampleRowViewModel> Samples { get; set; } = [];

    // Index of the given sample's value for this result item, or -1 if absent. Needed because the
    // table renders Result Items as columns and Samples as rows, while each sample's values are a
    // flat list in the same order as ResultItems (normally a direct index match, but this is the
    // safe lookup used by the view so a mismatch never throws).
    public int ValueIndexFor(int sampleIndex, Guid testResultItemId) =>
        Samples[sampleIndex].Values.FindIndex(v => v.TestResultItemId == testResultItemId);
}

// IsRange is a TEST-LEVEL toggle shared by every sample under this Test - legacy's
// CheckboxResult_CheckChanged sets Test.TestResultItems[x].IsRange directly, not per sample.
// A FormatString starting with "^" is a regex pattern (free-text box); anything else is a
// "|"-delimited list of dropdown options - legacy BuildResultCellContents' exact rule.
public sealed class IntendedResultsResultItemViewModel
{
    public Guid TestResultItemId { get; set; }

    public string? Name { get; set; }

    public string? FormatString { get; set; }

    public string? ReturnValueType { get; set; }

    public bool IsRange { get; set; }

    public bool IsDropdown => !(FormatString ?? string.Empty).StartsWith('^');

    public IReadOnlyList<string> DropdownOptions =>
        IsDropdown ? (FormatString ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries) : [];
}

// Method Items have exactly one value for the whole distribution (not per sample) - legacy stores
// IntendedResult directly on the Test's TestMethodItem. No RequiredFieldValidator exists on
// legacy's BuildMethodCellContents, so this is deliberately optional - see IntendedResultsValidator.
public sealed class IntendedResultsMethodItemViewModel
{
    public Guid TestMethodItemId { get; set; }

    public string? Name { get; set; }

    public string? FormatString { get; set; }

    public string? ReturnValueType { get; set; }

    public string? IntendedResult { get; set; }

    public bool IsDropdown => !(FormatString ?? string.Empty).StartsWith('^');

    public IReadOnlyList<string> DropdownOptions =>
        IsDropdown ? (FormatString ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries) : [];
}

public sealed class IntendedResultsSampleRowViewModel
{
    public Guid SampleId { get; set; }

    public string? SampleNumber { get; set; }

    public string? InternalDescription { get; set; }

    // Sample Allocation for THIS test - read-only here (set on Sample Numbers). Legacy disables
    // every Result Item input for this sample/test pair when false; the round-tripped value lets
    // the server skip diffing/validating a cell legacy would never have posted back.
#pragma warning disable S6964
    public bool IsApplicable { get; set; }
#pragma warning restore S6964

    public List<IntendedResultsSampleValueViewModel> Values { get; set; } = [];
}

public sealed class IntendedResultsSampleValueViewModel
{
#pragma warning disable S6964
    public Guid SampleResultItemId { get; set; }

    public Guid TestResultItemId { get; set; }
#pragma warning restore S6964

    // No RequiredFieldValidator and no StringLength on legacy's BuildResultCellContents - only a
    // RegularExpressionValidator that never fires on an empty value, so these stay optional.
    public string? IntendedResult { get; set; }

    public string? IntendedResult2 { get; set; }
}
