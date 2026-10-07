namespace PTL.Contracts.Scheme;

// Shared by POST /api/schemes (create) and PUT /api/schemes/{id} (update) - field set matches
// SchemeResponse minus server-generated/system-managed values (SchemeId, SharedId, LastModified,
// IsReadOnly, SampleNoSequence - joined from tblSchedule, never a spiScheme/spuScheme parameter).
// See docs/analysis/scheme-analysis.md. RequiresAssessment is accepted on update (spuScheme always
// requires a value) but SchemeService overwrites it with the existing persisted value on update -
// mirrors the legacy Scheme.aspx.vb behaviour of disabling the checkbox once a SchemeId exists.
public sealed record SchemeRequest(
    int YearId,
    string Identifier,
    string Name,
    Guid ScheduleId,
    Guid ScheduleCodeId,
    DateTime? StartDate,
    bool DistributionMonthApr,
    bool DistributionMonthMay,
    bool DistributionMonthJun,
    bool DistributionMonthJul,
    bool DistributionMonthAug,
    bool DistributionMonthSep,
    bool DistributionAsAvailable,
    bool DistributionMonthOct,
    bool DistributionMonthNov,
    bool DistributionMonthDec,
    bool DistributionMonthJan,
    bool DistributionMonthFeb,
    bool DistributionMonthMar,
    int WeekNumber,
    Guid DayOfWeekId,
    int NumberOfSamples,
    string SampleOrigin,
    int Deadline,
    string Subcontractor,
    bool CombinedPackaging,
    Guid? Postage,
    string? CustomsVolume,
    string SamplePackingInstructions,
    bool RequiresAssessment,
    bool CommentsRequired,
    bool Pilot,
    bool LimitedSampleAvailability,
    bool Accredited,
    bool NoVLALabs,
    bool ComerciallyAvailable,
    string? CustomsDescription,
    bool DataConsentDeclarationActive,
    string? DataConsentDeclarationText,
    string Instructions,
    bool DateOfReceipt,
    bool StorageConditions,
    bool ConditionOnReceipt,
    Guid? TestConsultant1,
    Guid? TestConsultant2,
    Guid? TestConsultant3,
    Guid? TestConsultantTabulationId,
    bool UseExternalReference,
    bool StoreRatings,
    Guid? Assessor1,
    Guid? Assessor2,
    Guid? Assessor3,
    Guid? Assessor4,
    string? StandardTabulationText,
    // Details tab currency pricing grid. Optional so existing callers remain source-compatible;
    // an empty list leaves tlnkSchemeCurrency untouched.
    IReadOnlyList<SchemeCurrencyPriceRequest>? Prices = null,
    // Viewers tab. Null is treated as "no viewers", so every caller must send the full list.
    IReadOnlyList<Guid>? ViewerIds = null,
    // Tests tab. Null is treated as "no tests", so every caller must send the full tree.
    IReadOnlyList<SchemeTestRequest>? Tests = null,
    // Results Tabulations tab. Null is treated as "no tabulations".
    IReadOnlyList<SchemeTabulationRequest>? Tabulations = null,
    // Set only by a Renew draft's Create post, to keep the new scheme in its existing family
    // instead of starting a new one. Null/omitted on every other Create request.
    Guid? SharedId = null);

public sealed record SchemeCurrencyPriceRequest(Guid SchemeCurrencyId, Guid CurrencyId, decimal Price);
