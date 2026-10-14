namespace PTL.Core.Distribution;

public interface IDistributionRepository
{
    Task<IReadOnlyList<DistributionMonthSummaryEntity>> GetMonthlyDistributionSummariesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionMonthYearEntity>> GetDistributionYearsAsync(CancellationToken cancellationToken = default);

    // spgMonthlyDistribution - the Monthly Distributions (Scheduling) screen's data source.
    // Returns null if the month has not been initialised (no tblMonthlyDistribution row yet).
    Task<(Guid MonthlyDistributionId, IReadOnlyList<MonthlyDistributionSchemeEntity> Schemes)?> GetMonthlyDistributionSchedulesAsync(
        int yearId, int monthId, CancellationToken cancellationToken = default);

    // spuMonthlyDistributionScheme x N, in a single transaction - legacy saves the whole page's
    // scheme list as one logical unit (the "Dates" ValidationGroup blocks the postback entirely if
    // any row is invalid, so either every row is saved or none are).
    Task UpdateMonthlyDistributionSchemesAsync(IReadOnlyList<MonthlyDistributionSchemeEntity> schemes, CancellationToken cancellationToken = default);

    // spgMonthlyDistributionScheme - a single scheme distribution plus its participants, legacy
    // MonthlyDistributionScheme.FetchMonthlyDistributionSchemeChild. Supplies the Sample Numbers
    // screen's heading and "Number of each Sample required" total. Returns null when the id does
    // not resolve (including when the distribution falls outside the seven-year retention window).
    Task<MonthlyDistributionSchemeEntity?> GetMonthlyDistributionSchemeAsync(
        Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    // spgIntendedResults - every result set behind legacy IntendedResultsSet.Fetch. Returns null
    // when the distribution scheme row itself is not returned.
    Task<IntendedResultsData?> GetIntendedResultsAsync(
        Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    // spuIntendedResults + spuSample x N + spuSampleTest x N in a single transaction, mirroring
    // IntendedResultsSet.DataPortal_Update's TransactionScope. spiAuditSample runs immediately
    // BEFORE each audited sample's spuSample, because the procedure records the sample's CURRENT
    // (pre-update) Tabulation Description. spiSetupSampleReceipt is executed last, and only for
    // assessment schemes (legacy SetupSampleReceipt.Execute) - it is enlisted in the same
    // transaction so a failed receipt setup cannot leave half-saved sample numbers.
    Task UpdateSampleNumbersAsync(SampleNumbersWrite write, CancellationToken cancellationToken = default);

    // Legacy IntendedResultsSet.DataPortal_Create + Insert: materialises the distribution's own
    // copy of the Scheme structure the first time Sample Numbers is opened. Allocates the sample
    // numbers (spgNextSampleNumber) and runs the whole insert chain in ONE transaction, so a
    // failure cannot leave a half-built distribution or a consumed-but-unused number sequence.
    // Returns false when another request created the structure first - the caller then just
    // re-reads it. HasSampleNumbersDefined/HasIntendedResults stay FALSE: the structure is a
    // draft until the user presses Save.
    Task<bool> CreateDistributionStructureAsync(
        Guid monthlyDistributionSchemeId,
        Guid scheduleId,
        DateTime sampleNumberDate,
        DistributionStructure distributionStructure,
        CancellationToken cancellationToken = default);

    // spuIntendedResults + spuDistributionTestMethodItem x N + spuDistributionTestResultItem x N +
    // spuSampleResultItem x N in a single transaction, mirroring IntendedResultsSet.
    // DataPortal_Update (mTests.Update then mSamples.Update). spiAuditDistributionTestMethodItem /
    // spiAuditSampleResultItem each run immediately BEFORE their row's own spu* call, because the
    // audit procedure records the row's CURRENT (pre-update) value.
    Task UpdateIntendedResultsAsync(IntendedResultsWrite write, CancellationToken cancellationToken = default);

    // spgMonthlyDistributionScheme - all 3 result sets (scheme header, Participants,
    // AdditionalParticipants), legacy DistributionParticipantList.aspx's LoadMonthlyDistributionScheme.
    // Returns null when the id does not resolve (including outside the seven-year retention window).
    Task<DistributionParticipantsData?> GetDistributionParticipantsAsync(
        Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    // spuDistributionParticipant - legacy DistributionParticipant.SetNewDeadlineDate followed by
    // MonthlyDistributionScheme.Save() (CSLA only sends an UPDATE for the one dirty child).
    Task UpdateParticipantDeadlineAsync(DistributionParticipantEntity participant, CancellationToken cancellationToken = default);

    // spiMonthlyDistributionScheme + spiDistributionParticipant x N in a single transaction -
    // legacy MonthlyDistribution.RepeatDistribution followed by MonthlyDistribution.Save(). The
    // suffix/new participant rows are computed by the caller (IDistributionParticipantsService)
    // from a fresh GetMonthlyDistributionSchedulesAsync read - no new stored procedure is used.
    Task RepeatDistributionAsync(
        MonthlyDistributionSchemeEntity newScheme,
        IReadOnlyList<DistributionParticipantEntity> newParticipants,
        CancellationToken cancellationToken = default);

    // spgPreparationSheet - the Sample Preparation screen's scheme+sample listing for a
    // distribution month, legacy PreparationSheet.FetchPreparationSheet. Returns null when the
    // header row itself is not found.
    Task<PreparationSheetData?> GetPreparationSheetAsync(Guid monthlyDistributionId, CancellationToken cancellationToken = default);

    // spuMonthlyDistributionSchemePrep x N - legacy MonthlyDistributionSchemeCollection.Update,
    // called once per dirty scheme (only schemes with at least one newly-set stage are passed in).
    Task UpdatePreparationAsync(IReadOnlyList<PreparationSchemeEntity> schemes, CancellationToken cancellationToken = default);
}
