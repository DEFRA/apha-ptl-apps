namespace PTL.Contracts.Distribution;

// Monthly Distributions scheduling screen (legacy Scheduling/monthlys.aspx) - see
// docs/analysis/distribution-analysis.md and docs/migration/distribution-migration.md.
public sealed record MonthlyDistributionResponse(
    Guid? MonthlyDistributionId,
    int YearId,
    int MonthId,
    IReadOnlyList<MonthlyDistributionSchemeResponse> Schemes);

// One scheme's row on the Monthly Distributions screen.
public sealed record MonthlyDistributionSchemeResponse(
    Guid MonthlyDistributionSchemeId,
    Guid SchemeId,
    string SchemeIdentifier,
    string SchemeName,
    string DistributionReferenceFull,
    DateTime DistributionDate,
    DateTime OverseasPostingDate,
    DateTime DeadlineDate,
    DateTime ResultsIssueTargetDate,
    int ParticipantCount,
    int TotalSetsOfSamplesRequired,
    bool HasSampleNumbersDefined,
    bool HasIntendedResults,
    bool IsCancelled,
    bool IsAsAvailable);

// One scheme row's posted dates/cancellation state (legacy: Save/Apply posts every row at once;
// the server round-trips every other field - Comments/HasIntendedResults/etc. - unchanged).
public sealed record MonthlyDistributionScheduleRowRequest(
    Guid MonthlyDistributionSchemeId,
    DateTime DistributionDate,
    DateTime OverseasPostingDate,
    DateTime DeadlineDate,
    DateTime ResultsIssueTargetDate,
    bool IsCancelled);

// Legacy validates the WHOLE page as one ValidationGroup before Save/Apply runs - if any row
// fails, NOTHING is persisted. FieldErrorsBySchemeId is therefore only ever populated when
// Success is false.
public sealed record MonthlyDistributionScheduleSaveResult(
    bool Success,
    IReadOnlyDictionary<Guid, string[]> FieldErrorsBySchemeId);
