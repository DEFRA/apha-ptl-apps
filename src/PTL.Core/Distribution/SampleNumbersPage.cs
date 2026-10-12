namespace PTL.Core.Distribution;

// The Sample Numbers screen (legacy Scheduling/SampleNumbers.aspx) as a single read model.
public sealed record SampleNumbersPage(
    Guid MonthlyDistributionSchemeId,
    string DistributionReferenceFull,
    string SchemeIdentifier,
    string SchemeName,
    int NumberOfSetsRequired,
    string Comments,
    bool RequiresAssessment,
    bool IsReadyForAssessment,
    IReadOnlyList<SampleNumberRow> Samples,
    IReadOnlyList<SampleNumbersTest> Tests);

// One row of the Sample Number / Descriptions table, plus its per-test Sample Allocation state.
public sealed record SampleNumberRow(
    Guid SampleId,
    string SampleNumber,
    string InternalDescription,
    string ExternalDescription,
    string TabulationDescription,
    string TestConsultantsDescription,
    IReadOnlyList<SampleAllocation> Allocations);

// tblSampleTest: one per sample/test pair. IsApplicable is the legacy Sample Allocation tick box.
public sealed record SampleAllocation(Guid SampleTestId, Guid TestId, bool IsApplicable);

// One tab on the "Sample Allocation, Method Items and Result Items for each Test" panel.
public sealed record SampleNumbersTest(
    Guid TestId,
    string TestName,
    IReadOnlyList<string> TestResultItemNames,
    IReadOnlyList<string> TestMethodItemNames,
    IReadOnlyList<string> CategoryItemNames);

// The posted form: every sample row plus the page-level Comments box. AuditWho/AuditWhy carry
// legacy Audit.aspx's captured reason; AuditWhy is empty until the user has been through it.
public sealed record SampleNumbersUpdate(
    string Comments,
    IReadOnlyList<SampleNumberRowUpdate> Samples,
    string AuditWho = "",
    string AuditWhy = "");

public sealed record SampleNumberRowUpdate(
    Guid SampleId,
    string SampleNumber,
    string InternalDescription,
    string ExternalDescription,
    string TabulationDescription,
    string TestConsultantsDescription,
    IReadOnlyList<SampleAllocationUpdate> Allocations);

public sealed record SampleAllocationUpdate(Guid SampleTestId, bool IsApplicable);

// Everything one legacy IntendedResultsSet.Save() writes. Only the dirty samples and dirty
// sample tests are present; AuditedSampleIds are the samples whose Tabulation Description changed
// and therefore need a tblAuditSample row written before their tblSample row is overwritten.
public sealed record SampleNumbersWrite(
    IntendedResultsHeaderEntity Header,
    IReadOnlyList<DistributionSampleEntity> Samples,
    IReadOnlyList<DistributionSampleTestEntity> SampleTests,
    IReadOnlyList<Guid> AuditedSampleIds,
    string AuditWho,
    string AuditWhy,
    bool SetupSampleReceipt);

// Legacy validates the whole page as one ValidationGroup, so FieldErrorsBySampleId is only ever
// populated when Success is false and nothing at all has been written. AuditReasonRequired is the
// legacy Audit.aspx diversion: the edit is valid but cannot be saved until a reason is supplied.
public sealed record SampleNumbersSaveOutcome(
    bool Success,
    IReadOnlyList<SampleNumbersError> PageErrors,
    IReadOnlyDictionary<Guid, IReadOnlyList<SampleNumbersError>> FieldErrorsBySampleId,
    bool AuditReasonRequired = false,
    string? AuditObjectName = null)
{
    public static SampleNumbersSaveOutcome Succeeded() =>
        new(true, [], new Dictionary<Guid, IReadOnlyList<SampleNumbersError>>());

    public static SampleNumbersSaveOutcome Failed(
        IReadOnlyList<SampleNumbersError> pageErrors,
        IReadOnlyDictionary<Guid, IReadOnlyList<SampleNumbersError>> fieldErrors) =>
        new(false, pageErrors, fieldErrors);

    public static SampleNumbersSaveOutcome NeedsAuditReason(string auditObjectName) =>
        new(false, [], new Dictionary<Guid, IReadOnlyList<SampleNumbersError>>(), true, auditObjectName);
}
