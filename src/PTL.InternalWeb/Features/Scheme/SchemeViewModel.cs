using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.Contracts.Scheme;
using PTL.Core.Scheme;
using CoreScheme = PTL.Core.Scheme.Scheme;
using CoreSchemeIdentifier = PTL.Core.Scheme.SchemeIdentifier;

namespace PTL.InternalWeb.Features.Scheme;

// Search-criteria portion of the Index view, bound directly from the query string.
public sealed record SchemeSearchViewModel(int YearId, string? SearchTerm, int Page, int PageSize);

public sealed record SchemeListViewModel(
    SchemeSearchViewModel Search,
    int TotalCount,
    IEnumerable<SelectListItem> YearOptions,
    IReadOnlyList<SchemeSummaryResponse> Schemes);

// One row of the Details tab's currency pricing grid. CurrencyName/CurrencySymbol are display
// only; SchemeCurrencyId is Guid.Empty for a currency the scheme has no price row for yet.
public sealed class SchemeCurrencyPriceViewModel
{
    public Guid SchemeCurrencyId { get; set; }

    public Guid CurrencyId { get; set; }

    public string CurrencyName { get; set; } = string.Empty;

    public string CurrencySymbol { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a price for every currency")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero")]
    public decimal? Price { get; set; }
}

// The Tests tab tree. Legacy stages the whole tree in ViewState and writes it only on Save, so
// Add/Remove/Up/Down post back to the same page, mutate this model and re-render - nothing
// touches the database until Save. Ids are Guid.Empty until an item has been persisted.
public sealed class SchemeTestViewModel
{
    public Guid TestId { get; set; }
    public Guid TestTypeId { get; set; }
    public string TestType { get; set; } = string.Empty;

    public List<SchemeTestItemViewModel> ResultItems { get; set; } = [];
    public List<SchemeTestItemViewModel> MethodItems { get; set; } = [];
    public List<SchemeCategoryItemViewModel> Categories { get; set; } = [];
}

public sealed class SchemeTestItemViewModel
{
    public Guid ItemId { get; set; }
    public Guid ItemTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class SchemeCategoryItemViewModel
{
    public Guid CategoryItemId { get; set; }
    public Guid CategoryItemTypeId { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<SchemeTestItemViewModel> Criteria { get; set; } = [];
}

// One column of the Results Tabulations grid. Availability is posted as a single three-way
// choice and expanded into the two stored flags, matching legacy's dropdown.
public sealed class SchemeTabulationViewModel
{
    public Guid TabulationId { get; set; }

    [Required(ErrorMessage = "A name for the Tabulation is required")]
    [StringLength(50, ErrorMessage = "Tabulation name must not exceed 50 characters")]
    public string Name { get; set; } = string.Empty;

#pragma warning disable S6964
    public bool IntendedResultsOnly { get; set; }
    public bool SingleParticipantTabulation { get; set; }
    public bool ShowRatings { get; set; }
#pragma warning restore S6964

    public SchemeTabulationAvailability Availability { get; set; } = SchemeTabulationAvailability.All;

    public List<Guid> ResultItemIds { get; set; } = [];
    public List<Guid> MethodItemIds { get; set; } = [];
}

public enum SchemeTabulationAvailability
{
    All,
    ParticipantsOnly,
    ViewersAndTestConsultantsOnly,
}

// One cell of the Results Tabulations inclusion matrix.
public sealed record SchemeTabulationItemCheckboxViewModel(string FieldName, Guid ItemId, bool Selected, string Label)
{
    public string CheckboxId { get; } = $"{FieldName.Replace("[", "_", StringComparison.Ordinal).Replace("]", "_", StringComparison.Ordinal).Replace(".", "_", StringComparison.Ordinal)}{ItemId:N}";
}

// Drives _SchemeTestItemList.cshtml, which renders the Results, Methods and Criteria lists -
// they differ only in labels, field prefix and command names.
public sealed record SchemeTestItemListViewModel(
    string Heading,
    List<SchemeTestItemViewModel> Items,
    string FieldPrefix,
    string AddCommand,
    string RemoveCommand,
    string MoveUpCommand,
    string MoveDownCommand,
    string AddLabel,
    string EmptyMessage,
    string PickerId,
    IEnumerable<SelectListItem> TypeOptions);

// Shared by Create.cshtml and Edit.cshtml, which render it across the seven legacy tabs
// (Details, Tests, Distribution Level Data, Results Tabulations, Test Consultants, Assessors,
// Viewers) defined by Scheme.aspx's TabContainer1. Validation attributes mirror
// PTL.Core.Scheme.SchemeValidator (see docs/analysis/scheme-analysis.md, "Validation Rules") so
// invalid input is rejected before it ever reaches PTL.Api. The API's SchemeValidator re-validates
// the same rules server-side as the authoritative source of truth.
//
// Distribution month order matches Scheme.aspx's Details tab exactly (Apr..Sep, AsAvailable,
// Oct..Mar - financial year order, not calendar order).
public sealed class SchemeFormViewModel : IValidatableObject
{
    public Guid? SchemeId { get; set; }

    // Read-only display fields, not posted back.
    public Guid? SharedId { get; set; }

    public bool? IsReadOnly { get; set; }

    public DateTime? LastModified { get; set; }

    public int? SampleNoSequence { get; set; }

    [Required(ErrorMessage = "Enter a year")]
    public int? YearId { get; set; }

    // Display only - legacy renders the year as a read-only textbox, because the entry point
    // (Create for Current Year / Create for Next Year) decides it, not the user.
    public string YearLabel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the scheme identifier")]
    [StringLength(6, ErrorMessage = "Identifier must not exceed 6 characters")]
    [RegularExpression("^([Pp][Tt])?[0-9]{4}$", ErrorMessage = "Identifier must be in the format PT0000 or 0000")]
    public string? Identifier { get; set; }

    [Required(ErrorMessage = "Enter the scheme name")]
    [StringLength(100, ErrorMessage = "Name must not exceed 100 characters")]
    public string? Name { get; set; }

    [DataType(DataType.Date)]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "Select a schedule")]
    public Guid? ScheduleId { get; set; }

    public IEnumerable<SelectListItem> ScheduleOptions { get; set; } = [];

    [Required(ErrorMessage = "Select a schedule code")]
    public Guid? ScheduleCodeId { get; set; }

    public IEnumerable<SelectListItem> ScheduleCodeOptions { get; set; } = [];

    // Legacy locks a month checkbox once distribution for that month has been initialised
    // (fnIsDistributionNotDefined, surfaced as fldCanEditXxx on spgSchemeBySchemeId). A new
    // scheme has nothing initialised, so every month defaults to editable.
    public bool CanEditJan { get; set; } = true;
    public bool CanEditFeb { get; set; } = true;
    public bool CanEditMar { get; set; } = true;
    public bool CanEditApr { get; set; } = true;
    public bool CanEditMay { get; set; } = true;
    public bool CanEditJun { get; set; } = true;
    public bool CanEditJul { get; set; } = true;
    public bool CanEditAug { get; set; } = true;
    public bool CanEditSep { get; set; } = true;
    public bool CanEditOct { get; set; } = true;
    public bool CanEditNov { get; set; } = true;
    public bool CanEditDec { get; set; } = true;

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

    [Required(ErrorMessage = "Enter the week number")]
    public int? WeekNumber { get; set; }

    // Legacy DropDownWeekNo is a fixed 1-4 list, not a free-text box.
    public static IReadOnlyList<SelectListItem> WeekNumberOptions { get; } =
    [
        new("1", "1"),
        new("2", "2"),
        new("3", "3"),
        new("4", "4"),
    ];

    [Required(ErrorMessage = "Select a day of week")]
    public Guid? DayOfWeekId { get; set; }

    public IEnumerable<SelectListItem> DayOfWeekOptions { get; set; } = [];

    [Required(ErrorMessage = "Enter the number of samples")]
    [Range(1, 999, ErrorMessage = "Number of samples must be between 1 and 999")]
    public int? NumberOfSamples { get; set; }

    [Required(ErrorMessage = "Enter the sample origin")]
    [StringLength(50, ErrorMessage = "Sample origin must not exceed 50 characters")]
    public string? SampleOrigin { get; set; }

    [Required(ErrorMessage = "Enter the deadline")]
    [Range(1, 999, ErrorMessage = "Deadline must be between 1 and 999")]
    public int? Deadline { get; set; }

    [StringLength(50, ErrorMessage = "Subcontractor must not exceed 50 characters")]
    public string? Subcontractor { get; set; }

    // See the S6964 suppression note above the distribution-month block - same HTML-checkbox
    // reasoning applies to every bool property below.
#pragma warning disable S6964
    public bool CombinedPackaging { get; set; }

    // Populated by SchemeController from /api/lookups/postage-pricing-plans?year=.
    public Guid? Postage { get; set; }

    public IEnumerable<SelectListItem> PostageOptions { get; set; } = [];

    [Required(ErrorMessage = "Enter the customs volume")]
    [StringLength(20, ErrorMessage = "Customs volume must not exceed 20 characters")]
    public string? CustomsVolume { get; set; }

    [StringLength(2000, ErrorMessage = "Sample packing instructions must not exceed 2000 characters")]
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

    [Required(ErrorMessage = "Enter the customs description")]
    [StringLength(500, ErrorMessage = "Customs description must not exceed 500 characters")]
    public string? CustomsDescription { get; set; }

#pragma warning disable S6964
    public bool DataConsentDeclarationActive { get; set; }
#pragma warning restore S6964

    [StringLength(500, ErrorMessage = "Consent text must not exceed 500 characters")]
    public string? DataConsentDeclarationText { get; set; }

    // Additional configuration - see class remarks above.
    [Required(ErrorMessage = "Enter the instructions")]
    [StringLength(50000, ErrorMessage = "Instructions must not exceed 50,000 characters")]
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

    [StringLength(500, ErrorMessage = "Standard tabulation text must not exceed 500 characters")]
    public string? StandardTabulationText { get; set; }

    // Test Consultants tab. The Primary list is internal consultants only (legacy populates it
    // from spgaUserAllTestConsultant's first result set); Deputy and Secondary also offer
    // external consultants. Both lists carry a "None" entry, as legacy does.
    public IEnumerable<SelectListItem> PrimaryTestConsultantOptions { get; set; } = [];

    public IEnumerable<SelectListItem> TestConsultantOptions { get; set; } = [];

    // Assessors tab.
    public IEnumerable<SelectListItem> AssessorOptions { get; set; } = [];

    // Viewers tab - every viewer on record, and the ones assigned to this scheme.
    public IReadOnlyList<SelectListItem> ViewerOptions { get; set; } = [];

    public List<Guid> ViewerIds { get; set; } = [];

    // Details tab currency pricing grid - one row per currency, in tblCurrency display order.
    public List<SchemeCurrencyPriceViewModel> Prices { get; set; } = [];

    // Tests tab tree, staged in the form until Save.
    public List<SchemeTestViewModel> Tests { get; set; } = [];

    // Results Tabulations tab, also staged until Save.
    public List<SchemeTabulationViewModel> Tabulations { get; set; } = [];

    // Posted by the "Add New Tabulation" control; not part of the saved scheme.
    public string? NewTabulationName { get; set; }

    // Year-scoped "Add" dropdown options for the Tests tab.
    public IEnumerable<SelectListItem> TestTypeOptions { get; set; } = [];
    public IEnumerable<SelectListItem> ResultItemTypeOptions { get; set; } = [];
    public IEnumerable<SelectListItem> MethodItemTypeOptions { get; set; } = [];
    public IEnumerable<SelectListItem> CategoryItemTypeOptions { get; set; } = [];
    public IEnumerable<SelectListItem> CriterionItemTypeOptions { get; set; } = [];

    // Forwards only PTL.Core.Scheme.SchemeValidator's domain/cross-field rules (distribution XOR,
    // conditional consent text, Test Consultant and Assessor selection) - every primitive rule is
    // already covered by the DataAnnotations above, so those are filtered out to avoid a
    // duplicate message.
    private static readonly HashSet<string> ForwardedDomainFields = new(StringComparer.Ordinal)
    {
        nameof(DistributionAsAvailable),
        nameof(DataConsentDeclarationText),
        nameof(TestConsultant1),
        nameof(TestConsultant2),
        nameof(TestConsultant3),
        nameof(Assessor1),
        nameof(Assessor2),
        nameof(Assessor3),
        nameof(Assessor4),
        "Tabulations",
        nameof(TestConsultantTabulationId),
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var scheme = new CoreScheme
        {
            Identifier = CoreSchemeIdentifier.Normalise(Identifier),
            Name = Name ?? string.Empty,
            Deadline = Deadline.GetValueOrDefault(),
            SampleOrigin = SampleOrigin ?? string.Empty,
            NumberOfSamples = NumberOfSamples.GetValueOrDefault(),
            Instructions = Instructions ?? string.Empty,
            CustomsDescription = CustomsDescription,
            CustomsVolume = CustomsVolume,
            Subcontractor = Subcontractor ?? string.Empty,
            SamplePackingInstructions = SamplePackingInstructions ?? string.Empty,
            StandardTabulationText = StandardTabulationText,
            RequiresAssessment = RequiresAssessment,
            TestConsultant1 = TestConsultant1,
            TestConsultant2 = TestConsultant2,
            TestConsultant3 = TestConsultant3,
            TestConsultantTabulationId = TestConsultantTabulationId,
            Tabulations = [.. Tabulations.Select(t => new PTL.Core.Scheme.SchemeTabulation { TabulationId = t.TabulationId, Name = t.Name, ShowRatings = t.ShowRatings })],
            Assessor1 = Assessor1,
            Assessor2 = Assessor2,
            Assessor3 = Assessor3,
            Assessor4 = Assessor4,
            DataConsentDeclarationActive = DataConsentDeclarationActive,
            DataConsentDeclarationText = DataConsentDeclarationText,
            DistributionMonthJan = DistributionMonthJan,
            DistributionMonthFeb = DistributionMonthFeb,
            DistributionMonthMar = DistributionMonthMar,
            DistributionMonthApr = DistributionMonthApr,
            DistributionMonthMay = DistributionMonthMay,
            DistributionMonthJun = DistributionMonthJun,
            DistributionMonthJul = DistributionMonthJul,
            DistributionMonthAug = DistributionMonthAug,
            DistributionMonthSep = DistributionMonthSep,
            DistributionMonthOct = DistributionMonthOct,
            DistributionMonthNov = DistributionMonthNov,
            DistributionMonthDec = DistributionMonthDec,
            DistributionAsAvailable = DistributionAsAvailable
        };

        var result = SchemeValidator.Validate(scheme);
        foreach (var error in result.Errors)
        {
            if (!ForwardedDomainFields.Contains(error.Field))
            {
                continue;
            }

            if (error.Field == nameof(DataConsentDeclarationText) && error.Message.Contains("must not exceed", StringComparison.Ordinal))
            {
                continue;
            }

            yield return new ValidationResult(error.Message, [error.Field]);
        }
    }
}
