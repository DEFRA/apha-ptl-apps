namespace PTL.Core.Participant;

// Round-trip entity for tlnkParticipantScheme (legacy PtaBusinessObjects.BusinessObjects.
// Contracts.ParticipantScheme) - backs both the Contract Items removal workflow and the full
// Participant Scheme Details/Add/Edit feature. Sourced from spgParticipantSchemeByParticipantSchemeId
// (get), spiParticipantScheme (create), spuParticipantScheme (update) - all pre-existing stored
// procedures (see docs/analysis/contract-analysis.md, docs/analysis/participant-analysis.md).
// The deployed spgParticipantSchemeByParticipantSchemeId (per Update Scripts/
// 20260401_NewImplementationForOverrideFeature.sql, which supersedes the older copy under
// Object Scripts/Stored Procedures/spg/) returns the real per-month fldIsOverrideJan.._Dec columns.
public sealed class ParticipantSchemeRecord
{
    public Guid ParticipantSchemeId { get; set; }

    public Guid ContractId { get; set; }

    public Guid ParticipantId { get; set; }

    public Guid SchemeId { get; set; }

    public Guid? GroupAddressId { get; set; }

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

    // Matches legacy dbo.fnIsDistributionNotPosted(Overseas) - false once that month's distribution
    // has already been posted, at which point CheckboxMonthX is locked unless the Override workflow
    // is used. Only populated by Get (always true for a brand-new, unsaved ParticipantScheme).
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

    public int NumberOfSetsRequired { get; set; }

    public string? ExternalReference { get; set; }

    public string? Contact { get; set; }

    public bool IsRemoved { get; set; }

    public bool ImportExportLicenceRequired { get; set; }

    public bool CustomsCertificateRequired { get; set; }

    public bool NonFeePaying { get; set; }

    public string? PackingInstructions { get; set; }

    public bool IsWeightedPricing { get; set; }

    public bool DataConsentDeclarationGiven { get; set; }

    // Per-month override flags (tlnkParticipantScheme.fldIsOverrideJan.._Dec, added by
    // 20260401_NewImplementationForOverrideFeature.sql) - matches legacy CheckboxIsOverrideJan.._Dec.
    // Sticky: once true for a month, LoadObjectFromForm's `(monthChecked And overrideMode) Or
    // existingIsOverrideX` formula never clears it back to false.
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

    // Display-only, populated on Get only (dbo.fnGetParticipantSchemePrice via the fetch procedure) -
    // never sent on Create/Update.
    public decimal Price { get; set; }

    // Display-only convenience strings already computed by the fetch procedure ("LabCode: LabName",
    // "Identifier: Name") - matches legacy TextboxParticipantDropDown.Text / TextboxSchemeName.Text.
    public string ParticipantDisplayName { get; set; } = string.Empty;

    public string SchemeDisplayName { get; set; } = string.Empty;
}

