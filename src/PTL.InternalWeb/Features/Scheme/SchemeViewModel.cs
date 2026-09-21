using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.Contracts.Scheme;

namespace PTL.InternalWeb.Features.Scheme;

// Search-criteria portion of the Index view, bound directly from the query string.
public sealed record SchemeSearchViewModel(int YearId, string? SearchTerm, int Page, int PageSize);

public sealed record SchemeListViewModel(
    SchemeSearchViewModel Search,
    int TotalCount,
    IEnumerable<SelectListItem> YearOptions,
    IReadOnlyList<SchemeSummaryResponse> Schemes);

// Shared by Create.cshtml and Edit.cshtml. Validation attributes mirror
// PTL.Core.Scheme.SchemeValidator (see docs/analysis/scheme-analysis.md, "Validation Rules") so
// invalid input is rejected client + server side before it ever reaches PTL.Api. The API's
// SchemeValidator re-validates the same rules server-side as the authoritative source of truth.
//
// Field order below matches Scheme.aspx's "Details" tab exactly (Apr..Sep, AsAvailable, Oct..Mar -
// financial year order, not calendar order) - see the Legacy -> Target mapping in this feature's
// implementation notes. Fields not visually located in the reviewed legacy screen (Instructions,
// DateOfReceipt/StorageConditions/ConditionOnReceipt, TestConsultant1-3,
// TestConsultantTabulationId, UseExternalReference, StoreRatings, Assessor1-4,
// StandardTabulationText) are grouped in a final "Additional configuration" section as a
// documented assumption pending confirmation against the full legacy page.
public sealed class SchemeFormViewModel : IValidatableObject
{
    public Guid? SchemeId { get; set; }

    // Read-only display fields, not posted back.
    public Guid? SharedId { get; set; }

    public bool? IsReadOnly { get; set; }

    public DateTime? LastModified { get; set; }

    public int? SampleNoSequence { get; set; }

    [Required(ErrorMessage = "Enter a year.")]
    public int? YearId { get; set; }

    // Populated by SchemeController before the view is rendered - see /api/lookups/years
    // (current + next year only, mirrors legacy DropDownYear behaviour).
    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    [Required(ErrorMessage = "Enter the scheme identifier.")]
    [StringLength(6, ErrorMessage = "Identifier must not exceed 6 characters.")]
    [RegularExpression("^PT[0-9]{4}$", ErrorMessage = "Identifier must match the format PT followed by 4 digits (e.g. PT1234).")]
    public string? Identifier { get; set; }

    [Required(ErrorMessage = "Enter the scheme name.")]
    [StringLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
    public string? Name { get; set; }

    [DataType(DataType.Date)]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "Select a schedule.")]
    public Guid? ScheduleId { get; set; }

    [Required(ErrorMessage = "Select a schedule code.")]
    public Guid? ScheduleCodeId { get; set; }

    // Distribution months - legacy screen order (financial year, Apr first).
    // S6964 (value-type controller-action input) suppressed for this block: these are HTML
    // checkboxes, where an unchecked box simply isn't posted and the framework's own
    // asp-for-generated hidden companion input already supplies "false" - non-nullable bool
    // defaulting to false on under-posting is the framework-intended behaviour, not a bug.
#pragma warning disable S6964
    public bool DistributionMonthApr { get; set; }
    public bool DistributionMonthMay { get; set; }
    public bool DistributionMonthJun { get; set; }
    public bool DistributionMonthJul { get; set; }
    public bool DistributionMonthAug { get; set; }
    public bool DistributionMonthSep { get; set; }
    public bool DistributionAsAvailable { get; set; }
    public bool DistributionMonthOct { get; set; }
    public bool DistributionMonthNov { get; set; }
    public bool DistributionMonthDec { get; set; }
    public bool DistributionMonthJan { get; set; }
    public bool DistributionMonthFeb { get; set; }
    public bool DistributionMonthMar { get; set; }
#pragma warning restore S6964

    [Required(ErrorMessage = "Enter the week number.")]
    public int? WeekNumber { get; set; }

    [Required(ErrorMessage = "Select a day of week.")]
    public Guid? DayOfWeekId { get; set; }

    [Required(ErrorMessage = "Enter the number of samples.")]
    [Range(1, 999, ErrorMessage = "Number of samples must be between 1 and 999.")]
    public int? NumberOfSamples { get; set; }

    [Required(ErrorMessage = "Enter the sample origin.")]
    [StringLength(50, ErrorMessage = "Sample origin must not exceed 50 characters.")]
    public string? SampleOrigin { get; set; }

    [Required(ErrorMessage = "Enter the deadline.")]
    [Range(1, 999, ErrorMessage = "Deadline must be between 1 and 999.")]
    public int? Deadline { get; set; }

    [StringLength(50, ErrorMessage = "Subcontractor must not exceed 50 characters.")]
    public string? Subcontractor { get; set; }

    // See the S6964 suppression note above the distribution-month block - same HTML-checkbox
    // reasoning applies to every bool property below.
#pragma warning disable S6964
    public bool CombinedPackaging { get; set; }

    // Populated by SchemeController from /api/lookups/postage-pricing-plans?year=.
    public Guid? Postage { get; set; }

    public IEnumerable<SelectListItem> PostageOptions { get; set; } = [];

    [StringLength(20, ErrorMessage = "Customs volume must not exceed 20 characters.")]
    public string? CustomsVolume { get; set; }

    [StringLength(2000, ErrorMessage = "Sample packing instructions must not exceed 2000 characters.")]
    public string? SamplePackingInstructions { get; set; }
#pragma warning restore S6964

    // Editable on Create only - disabled on Edit, mirroring Scheme.aspx.vb's
    // CheckboxRequiresAssessment.Enabled = False once a SchemeId exists.
#pragma warning disable S6964
    public bool RequiresAssessment { get; set; }

    public bool CommentsRequired { get; set; }
    public bool Pilot { get; set; }
    public bool LimitedSampleAvailability { get; set; }
    public bool Accredited { get; set; }
    public bool NoVLALabs { get; set; }
    public bool ComerciallyAvailable { get; set; }
#pragma warning restore S6964

    [StringLength(500, ErrorMessage = "Customs description must not exceed 500 characters.")]
    public string? CustomsDescription { get; set; }

#pragma warning disable S6964
    public bool DataConsentDeclarationActive { get; set; }
#pragma warning restore S6964

    [StringLength(500, ErrorMessage = "Consent text must not exceed 500 characters.")]
    public string? DataConsentDeclarationText { get; set; }

    // Additional configuration - see class remarks above.
    [Required(ErrorMessage = "Enter the instructions.")]
    [StringLength(50000, ErrorMessage = "Instructions must not exceed 50,000 characters.")]
    public string? Instructions { get; set; }

#pragma warning disable S6964
    public bool DateOfReceipt { get; set; }
    public bool StorageConditions { get; set; }
    public bool ConditionOnReceipt { get; set; }
    public Guid? TestConsultant1 { get; set; }
    public Guid? TestConsultant2 { get; set; }
    public Guid? TestConsultant3 { get; set; }
    public Guid? TestConsultantTabulationId { get; set; }
    public bool UseExternalReference { get; set; }
    public bool StoreRatings { get; set; }
#pragma warning restore S6964
    public Guid? Assessor1 { get; set; }
    public Guid? Assessor2 { get; set; }
    public Guid? Assessor3 { get; set; }
    public Guid? Assessor4 { get; set; }

    [StringLength(500, ErrorMessage = "Standard tabulation text must not exceed 500 characters.")]
    public string? StandardTabulationText { get; set; }

    // Preserves ValidateDistribution/ValidateDataConsentDeclaration.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasAnyMonth = DistributionMonthJan || DistributionMonthFeb || DistributionMonthMar
            || DistributionMonthApr || DistributionMonthMay || DistributionMonthJun
            || DistributionMonthJul || DistributionMonthAug || DistributionMonthSep
            || DistributionMonthOct || DistributionMonthNov || DistributionMonthDec;
        if (hasAnyMonth == DistributionAsAvailable)
        {
            yield return new ValidationResult("Select either specific distribution months or 'as available', but not both.", [nameof(DistributionAsAvailable)]);
        }

        if (DataConsentDeclarationActive && string.IsNullOrWhiteSpace(DataConsentDeclarationText))
        {
            yield return new ValidationResult("Enter the consent text when the Data Consent Declaration is active.", [nameof(DataConsentDeclarationText)]);
        }
    }
}
