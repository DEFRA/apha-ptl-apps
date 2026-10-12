using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.Distribution;

// Sample Numbers screen - legacy Scheduling/SampleNumbers.aspx. Reached from the Monthly
// Distributions (Scheduling) screen's "Define Sample Numbers"/"View Sample Numbers" link.
public sealed class SampleNumbersViewModel : IValidatableObject
{
    // S6964 suppressed across the hidden/round-tripped inputs: they are posted back verbatim so
    // the form can be redisplayed after a validation failure without losing the user's edits.
#pragma warning disable S6964
    public Guid MonthlyDistributionSchemeId { get; set; }

    public int YearId { get; set; }

    public int MonthId { get; set; }
#pragma warning restore S6964

    // Legacy LblDistribution: reference + " " + identifier + ": " + name.
    // Nullable deliberately: ASP.NET Core treats a non-nullable reference type as implicitly
    // [Required], and these are display-only values re-read from the API on every redisplay
    // rather than posted, so a non-nullable type would raise a bogus "field is required" error.
    public string? DistributionReferenceFull { get; set; }

    public string? SchemeIdentifier { get; set; }

    public string? SchemeName { get; set; }

    public string DistributionHeading => $"{DistributionReferenceFull} {SchemeIdentifier}: {SchemeName}";

    // Legacy LiteralNumberRequired - "Number of each Sample required". S6964 suppressed: this is
    // display-only, is never read from the post, and is always re-read from the API before the
    // form is redisplayed, so under-posting it has no effect.
#pragma warning disable S6964
    public int NumberOfSetsRequired { get; set; }
#pragma warning restore S6964

    // Optional on the legacy screen - its only validator is a 0-8000 character regex, there is
    // no RequiredFieldValidator. Nullable so the implicit-required rule cannot make it mandatory.
    [StringLength(8000, ErrorMessage = "Comments must not exceed 8000 characters")]
    [Display(Name = "Comments")]
    public string? Comments { get; set; }

#pragma warning disable S6964
    public bool RequiresAssessment { get; set; }

    // Legacy disables every Sample Allocation tick box once an assessment scheme has been
    // released to the assessor.
    public bool IsReadyForAssessment { get; set; }
#pragma warning restore S6964

    public bool AllocationEditable => !(RequiresAssessment && IsReadyForAssessment);

    public List<SampleNumberRowViewModel> Samples { get; set; } = [];

    public List<SampleNumbersTestViewModel> Tests { get; set; } = [];

    // Legacy Audit.aspx: changing a Tabulation Description on an existing sample diverts to a
    // separate page that demands a reason before the save is allowed to run. Once this is set the
    // page renders that step instead of the sample table.
#pragma warning disable S6964
    public bool RequiresAuditReason { get; set; }
#pragma warning restore S6964

    // Legacy IntendedResultsSet.AuditObjectName(), shown as the heading on Audit.aspx.
    public string? AuditObjectName { get; set; }

    // The command that triggered the audit diversion, so Save/Cancel on the audit step return to
    // the destination the user originally chose (legacy Session("AuditLeavePage")).
    public string? PendingCommand { get; set; }

    [StringLength(2000, ErrorMessage = "Input can not exceed 2000 characters")]
    [Display(Name = "Reason for these changes")]
    public string? AuditWhy { get; set; }

    // Index of the given sample's allocation for a test, or -1 when the pair has no tblSampleTest
    // row. Needed because the tabs iterate tests x samples while the inputs are bound by sample.
    public int AllocationIndexFor(int sampleIndex, Guid testId) =>
        Samples[sampleIndex].Allocations.FindIndex(a => a.TestId == testId);

    // Legacy Audit.aspx ValidatorAuditWhyRequired - only in force once the audit step is showing.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequiresAuditReason && string.IsNullOrWhiteSpace(AuditWhy))
        {
            yield return new ValidationResult(
                "The reason for these changes must be specified", [nameof(AuditWhy)]);
        }
    }
}

// Shown when the distribution's structure cannot be built from its Scheme, so Sample Numbers
// cannot be opened at all.
public sealed class SampleNumbersUnavailableViewModel
{
    public int YearId { get; set; }

    public int MonthId { get; set; }

    public IReadOnlyList<string> Errors { get; set; } = [];
}

public sealed class SampleNumberRowViewModel
{
#pragma warning disable S6964
    public Guid SampleId { get; set; }
#pragma warning restore S6964

    // The ONLY two mandatory fields on the legacy screen - SampleNumbers.aspx.vb creates a
    // RequiredFieldValidator for the Sample Number and the Internal Description, and for nothing
    // else. Every other field below is nullable so that ASP.NET Core's implicit-required rule for
    // non-nullable reference types cannot silently make it mandatory.
    [Required(ErrorMessage = "Enter the Sample Number")]
    [StringLength(50, ErrorMessage = "Sample Number must not exceed 50 characters")]
    [Display(Name = "Sample No.")]
    public string SampleNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the Internal Description")]
    [StringLength(100, ErrorMessage = "Internal Description must not exceed 100 characters")]
    [Display(Name = "Internal Description")]
    public string InternalDescription { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Tabulation Description must not exceed 500 characters")]
    [Display(Name = "Tabulation Description")]
    public string? TabulationDescription { get; set; }

    // Legacy field name is ExternalDescription; the label on screen is "Test Instruction".
    [StringLength(500, ErrorMessage = "Test Instruction must not exceed 500 characters")]
    [Display(Name = "Test Instruction")]
    public string? ExternalDescription { get; set; }

    [StringLength(500, ErrorMessage = "Description for TC must not exceed 500 characters")]
    [Display(Name = "Description for TC")]
    public string? TestConsultantsDescription { get; set; }

    public List<SampleAllocationViewModel> Allocations { get; set; } = [];
}

public sealed class SampleAllocationViewModel
{
#pragma warning disable S6964
    public Guid SampleTestId { get; set; }

    public Guid TestId { get; set; }

    public bool IsApplicable { get; set; }
#pragma warning restore S6964
}

// One tab of the "Sample Allocation, Method Items and Result Items for each Test" panel.
// Display-only and rebuilt from the API on every render, so nothing here is posted back.
public sealed class SampleNumbersTestViewModel
{
    public Guid TestId { get; set; }

    public string? TestName { get; set; }

    public IReadOnlyList<string> TestResultItemNames { get; set; } = [];

    public IReadOnlyList<string> TestMethodItemNames { get; set; } = [];

    public IReadOnlyList<string> CategoryItemNames { get; set; } = [];
}
