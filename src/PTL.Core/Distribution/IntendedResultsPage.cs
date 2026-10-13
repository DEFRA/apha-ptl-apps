namespace PTL.Core.Distribution;

// Intended Results screen (legacy Scheduling/IntendedResults.aspx). One vertically-stacked table
// per Test: Result Items as columns (one govuk table column group per item, optionally a Range
// pair), Samples as rows, then Method Items as test-level rows below (shared across every sample,
// not per-sample). RequiresAuditReason is legacy's IntendedResultsSet.RequireAudit - computed ONCE
// here, from the values as fetched, and must be carried unchanged through the whole edit session
// (round-tripped by the caller) rather than recomputed from what the user posts.
public sealed record IntendedResultsPage(
    Guid MonthlyDistributionSchemeId,
    string DistributionReferenceFull,
    string SchemeIdentifier,
    string SchemeName,
    bool RequiresAuditReason,
    IReadOnlyList<IntendedResultsTest> Tests);

public sealed record IntendedResultsTest(
    Guid TestId,
    string TestName,
    IReadOnlyList<IntendedResultsResultItem> ResultItems,
    IReadOnlyList<IntendedResultsMethodItem> MethodItems,
    IReadOnlyList<IntendedResultsSampleRow> Samples);

// IsRange is a TEST-LEVEL toggle shared by every sample under this Test, not a per-sample
// setting - legacy's CheckboxResult_CheckChanged sets Test.TestResultItems[x].IsRange directly.
public sealed record IntendedResultsResultItem(
    Guid TestResultItemId,
    string Name,
    string FormatString,
    string ReturnValueType,
    bool IsRange);

// Method Items have exactly one value for the whole distribution (not per sample) - legacy stores
// IntendedResult directly on the Test's TestMethodItem, never on a per-Sample child object.
public sealed record IntendedResultsMethodItem(
    Guid TestMethodItemId,
    string Name,
    string FormatString,
    string ReturnValueType,
    string IntendedResult);

public sealed record IntendedResultsSampleRow(
    Guid SampleId,
    string SampleNumber,
    string InternalDescription,
    // Sample Allocation for THIS test - read-only here (set on the Sample Numbers screen). Legacy
    // disables every Result Item input for this sample/test pair when false and blanks the display.
    bool IsApplicable,
    // One entry per IntendedResultsTest.ResultItems, same order, matched by TestResultItemId.
    IReadOnlyList<IntendedResultsSampleResultValue> Values);

public sealed record IntendedResultsSampleResultValue(
    Guid SampleResultItemId,
    Guid TestResultItemId,
    string IntendedResult,
    string IntendedResult2);

// The posted form.
public sealed record IntendedResultsUpdate(
    IReadOnlyList<IntendedResultsTestUpdate> Tests,
    // Round-tripped from the GET-time IntendedResultsPage.RequiresAuditReason - never recomputed
    // from this post, exactly mirroring legacy's Session-held IntendedResultsSet.
    bool RequiresAuditReason,
    string AuditWho,
    string AuditWhy);

public sealed record IntendedResultsTestUpdate(
    Guid TestId,
    IReadOnlyList<IntendedResultsResultItemUpdate> ResultItems,
    IReadOnlyList<IntendedResultsMethodItemUpdate> MethodItems,
    IReadOnlyList<IntendedResultsSampleRowUpdate> Samples);

public sealed record IntendedResultsResultItemUpdate(Guid TestResultItemId, bool IsRange);

public sealed record IntendedResultsMethodItemUpdate(Guid TestMethodItemId, string IntendedResult);

public sealed record IntendedResultsSampleRowUpdate(
    Guid SampleId,
    // Read-only on this screen (set on Sample Numbers) - round-tripped so the service can skip
    // diffing/validating a cell legacy would have rendered disabled, rather than mistaking a
    // disabled control's absent post data for "changed to blank".
    bool IsApplicable,
    IReadOnlyList<IntendedResultsSampleResultValueUpdate> Values);

public sealed record IntendedResultsSampleResultValueUpdate(
    Guid SampleResultItemId,
    string IntendedResult,
    string IntendedResult2);

// Legacy validates the whole page as one unit (Page.Validate()) - if anything fails, nothing is
// saved. Field errors are keyed by the TestResultItemId/TestMethodItemId/SampleResultItemId the
// error belongs to, since this screen has no single flat row list to index into.
// AuditReasonRequired mirrors legacy's unconditional Audit.aspx diversion when RequireAudit is
// true - crossed BEFORE anything is written, even when nothing in this particular save is dirty.
public sealed record IntendedResultsSaveOutcome(
    bool Success,
    IReadOnlyDictionary<Guid, IReadOnlyList<SampleNumbersError>> FieldErrorsByItemId,
    bool AuditReasonRequired = false,
    string? AuditObjectName = null)
{
    public static IntendedResultsSaveOutcome Succeeded() =>
        new(true, new Dictionary<Guid, IReadOnlyList<SampleNumbersError>>());

    public static IntendedResultsSaveOutcome Failed(IReadOnlyDictionary<Guid, IReadOnlyList<SampleNumbersError>> fieldErrors) =>
        new(false, fieldErrors);

    public static IntendedResultsSaveOutcome NeedsAuditReason(string auditObjectName) =>
        new(false, new Dictionary<Guid, IReadOnlyList<SampleNumbersError>>(), true, auditObjectName);
}

// Everything one legacy IntendedResultsSet.Save() writes, already reduced to the dirty subset.
// TestResultItem (IsRange) is never audited - legacy's TestResultItem is a plain BusinessBase, not
// AuditableBusinessBase, and Test.TestResultItems is not an [AuditableCollection].
public sealed record IntendedResultsWrite(
    IntendedResultsHeaderEntity Header,
    IReadOnlyList<DistributionTestMethodItemEntity> TestMethodItems,
    IReadOnlyList<Guid> AuditedTestMethodItemIds,
    IReadOnlyList<DistributionTestResultItemEntity> TestResultItems,
    IReadOnlyList<DistributionSampleResultItemEntity> SampleResultItems,
    IReadOnlyList<AuditedSampleResultItemChange> AuditedSampleResultItems,
    string AuditWho,
    string AuditWhy);

// SampleResultItem has two independently auditable properties (IntendedResult/IntendedResult2) -
// spiAuditSampleResultItem needs to know which one(s) actually changed for its @...Update bits.
public sealed record AuditedSampleResultItemChange(
    Guid SampleResultItemId,
    bool IntendedResultChanged,
    bool IntendedResult2Changed);
