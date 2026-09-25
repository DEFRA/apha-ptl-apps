using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.Contracts.Participant;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.Pagination;

namespace PTL.InternalWeb.Features.ParticipantScheme;

// Details.cshtml model - read-only summary, used both as the standalone "View" page and whenever
// IsReadOnly is true (matches legacy ParticipantScheme.aspx locking every control down when
// mContract.IsReadOnly or mParticipantScheme.IsRemoved).
public sealed record ParticipantSchemeDetailsViewModel(ParticipantSchemeResponse ParticipantScheme, Guid CustomerId, bool IsReadOnly);

// Shared by Create.cshtml and Edit.cshtml. Participant/Scheme selection only happens on Create -
// legacy hides DropDownParticipant/GridViewSchemes once mParticipantScheme.IsNew is false, so Edit
// only ever shows ParticipantDisplayName/SchemeDisplayName as plain text, never the pickers.
public sealed class ParticipantSchemeFormViewModel
{
    public Guid? ParticipantSchemeId { get; set; }

    public Guid ContractId { get; set; }

    public Guid CustomerId { get; set; }

    public int YearId { get; set; }

    public Guid? ParticipantId { get; set; }

    public string ParticipantDisplayName { get; set; } = string.Empty;

    public IEnumerable<SelectListItem> ParticipantOptions { get; set; } = [];

    public Guid? SchemeId { get; set; }

    public string SchemeDisplayName { get; set; } = string.Empty;

    // Populated on Create only, while no SchemeId has been chosen yet - matches legacy
    // GridViewSchemes (paged list of SchemeInfoCollection.FetchSchemeInfoCollectionByYearId).
    public string? SchemeSearchTerm { get; set; }

    public IReadOnlyList<SchemeSummaryResponse> SchemeOptions { get; set; } = [];

    public int SchemeOptionsTotalCount { get; set; }

    public int SchemeOptionsPage { get; set; } = 1;

    public PaginationModel SchemePagination => new()
    {
        CurrentPage = SchemeOptionsPage,
        PageSize = PaginationModel.DefaultPageSize,
        TotalRecords = SchemeOptionsTotalCount,
        Action = "Create",
        RouteValues = new Dictionary<string, object?> { ["contractId"] = ContractId, ["customerId"] = CustomerId, ["participantId"] = ParticipantId, ["schemeSearchTerm"] = SchemeSearchTerm }
    };

    public bool DistributionMonthJan { get; set; }

    public bool DistributionMonthFeb { get; set; }

    public bool DistributionMonthMar { get; set; }

    public bool DistributionMonthApr { get; set; }

    public bool DistributionMonthMay { get; set; }

    public bool DistributionMonthJun { get; set; }

    public bool DistributionMonthJul { get; set; }

    public bool DistributionMonthAug { get; set; }

    public bool DistributionMonthSep { get; set; }

    public bool DistributionMonthOct { get; set; }

    public bool DistributionMonthNov { get; set; }

    public bool DistributionMonthDec { get; set; }

    // Whether the scheme itself offers each month at all (PtaBusinessObjects.Schemes.Scheme.
    // DistributionMonthX) - a month checkbox is only ever shown/checkable when both this AND the
    // matching CanEditX below are true, matching legacy's `mScheme.DistributionMonthX And ...`.
    public bool SchemeDistributionMonthJan { get; set; } = true;

    public bool SchemeDistributionMonthFeb { get; set; } = true;

    public bool SchemeDistributionMonthMar { get; set; } = true;

    public bool SchemeDistributionMonthApr { get; set; } = true;

    public bool SchemeDistributionMonthMay { get; set; } = true;

    public bool SchemeDistributionMonthJun { get; set; } = true;

    public bool SchemeDistributionMonthJul { get; set; } = true;

    public bool SchemeDistributionMonthAug { get; set; } = true;

    public bool SchemeDistributionMonthSep { get; set; } = true;

    public bool SchemeDistributionMonthOct { get; set; } = true;

    public bool SchemeDistributionMonthNov { get; set; } = true;

    public bool SchemeDistributionMonthDec { get; set; } = true;

    // False once that month's distribution has already been posted (dbo.fnIsDistributionNotPosted) -
    // the checkbox is then locked unless the Override button is used, matching legacy exactly.
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

    public int? NumberOfSetsRequired { get; set; } = 1;

    public string? ExternalReference { get; set; }

    public string? Contact { get; set; }

    public bool ImportExportLicenceRequired { get; set; }

    public bool CustomsCertificateRequired { get; set; }

    public bool NonFeePaying { get; set; }

    public string? PackingInstructions { get; set; }

    // Mirrors legacy DdlPricingPlan ("0"=Weighted pricing plan, "1"=Pro rata, "2"=Full scheme
    // pricing). Only the options LoadPricingOptions would actually offer are exposed in
    // PricingPlanOptions - see ParticipantSchemeController.PopulatePricingPlanAsync.
    public string PricingPlan { get; set; } = "Weighted";

    public IEnumerable<SelectListItem> PricingPlanOptions { get; set; } = [];

    // False for contracts before the 2010/11 year (WeightedPricingYearCollection.PricingPlanExists) -
    // when false, legacy hides the Pricing Plan control entirely and forces Pro rata.
    public bool IsWeightedSchemeAvailable { get; set; } = true;

    public bool IsWeightedPricing { get; set; } = true;

    public bool DataConsentDeclarationGiven { get; set; }

    // Only rendered when the selected scheme's DataConsentDeclarationActive is true - matches
    // legacy's CheckBoxDataConsent/LblConsentBox visibility toggle.
    public bool SchemeRequiresDataConsent { get; set; }

    // Matches legacy CheckboxIsOverrideJan.._Dec - a read-only per-month indicator shown alongside
    // each month checkbox, backed by the real per-month tlnkParticipantScheme.fldIsOverrideJan.._Dec
    // columns (see ParticipantSchemeRecord).
    public bool IsOverrideJan { get; set; }

    public bool IsOverrideFeb { get; set; }

    public bool IsOverrideMar { get; set; }

    public bool IsOverrideApr { get; set; }

    public bool IsOverrideMay { get; set; }

    public bool IsOverrideJun { get; set; }

    public bool IsOverrideJul { get; set; }

    public bool IsOverrideAug { get; set; }

    public bool IsOverrideSep { get; set; }

    public bool IsOverrideOct { get; set; }

    public bool IsOverrideNov { get; set; }

    public bool IsOverrideDec { get; set; }

    // Hidden field toggled client-side by the "Override" button (matches legacy
    // HiddenFieldOverrideMode) - only used to decide whether a checked, previously-locked month
    // should be recorded as an override on save; never persisted itself.
    public bool OverrideModeActive { get; set; }

    public decimal Price { get; set; }

    public bool IsReadOnly { get; set; }

    // Matches legacy hidGroupAddressId/TextboxGroupAddress/TextboxGroupAddress1/TextboxGroupCountry
    // (Group Address section of ParticipantScheme.aspx). GroupAddressId is the only field actually
    // posted back - the other three are display-only, populated client-side when a group address is
    // selected from GroupAddressOptions, and server-side (in ParticipantSchemeController) whenever
    // the form is (re)rendered with an existing GroupAddressId.
    public Guid? GroupAddressId { get; set; }

    public string? GroupAddressIdentifier { get; set; }

    public string? GroupAddressAddress1 { get; set; }

    public string? GroupAddressCountry { get; set; }

    public IReadOnlyList<GroupAddressOptionViewModel> GroupAddressOptions { get; set; } = [];
}

// One row of the "Select a Group Address" dropdown - CountryName is resolved server-side (via
// ILookupApiClient.GetCountriesAsync) so the client-side "Select"/"Clear" JS never needs its own
// copy of the country list.
public sealed record GroupAddressOptionViewModel(Guid GroupAddressId, string Identifier, string Address1, string CountryName);

