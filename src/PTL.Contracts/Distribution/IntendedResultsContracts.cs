namespace PTL.Contracts.Distribution;

// Intended Results screen (legacy Scheduling/IntendedResults.aspx) - one table per Test, Result
// Items as columns, Samples as rows, Method Items as test-level rows shared across every sample.
public sealed record IntendedResultsResponse(
    Guid MonthlyDistributionSchemeId,
    string DistributionReferenceFull,
    string SchemeIdentifier,
    string SchemeName,
    bool RequiresAuditReason,
    IReadOnlyList<IntendedResultsTestResponse> Tests,
    // The remaining 5 are only used by Printable Results - snapshotted onto the distribution from
    // the Scheme once, at creation time (see IntendedResultsHeaderEntity).
    DateTime SchemeVersionDate = default,
    bool HasIntendedResults = false,
    bool DateOfReceipt = false,
    bool StorageConditions = false,
    bool ConditionOnReceipt = false,
    string Instructions = "");

public sealed record IntendedResultsTestResponse(
    Guid TestId,
    string TestName,
    IReadOnlyList<IntendedResultsResultItemResponse> ResultItems,
    IReadOnlyList<IntendedResultsMethodItemResponse> MethodItems,
    IReadOnlyList<IntendedResultsSampleRowResponse> Samples);

// IsRange is a TEST-LEVEL toggle shared by every sample under this Test, not per-sample.
// ExpectedLength is only used by Printable Results - legacy ResultsTable.GetColumnWidth's
// column-packing budget (ExpectedLength * 20 pixels per column).
public sealed record IntendedResultsResultItemResponse(
    Guid TestResultItemId, string Name, string FormatString, string ReturnValueType, bool IsRange, int ExpectedLength = 0);

// Method Items have exactly one value for the whole distribution, not per sample.
public sealed record IntendedResultsMethodItemResponse(
    Guid TestMethodItemId, string Name, string FormatString, string ReturnValueType, string IntendedResult);

public sealed record IntendedResultsSampleRowResponse(
    Guid SampleId,
    string SampleNumber,
    string InternalDescription,
    bool IsApplicable,
    IReadOnlyList<IntendedResultsSampleResultValueResponse> Values,
    // Only used by Printable Results' sample-instruction grid.
    string ExternalDescription = "");

public sealed record IntendedResultsSampleResultValueResponse(
    Guid SampleResultItemId, Guid TestResultItemId, string IntendedResult, string IntendedResult2);

public sealed record IntendedResultsSaveRequest(
    IReadOnlyList<IntendedResultsTestRequest> Tests,
    // Round-tripped from the GET response's RequiresAuditReason - never recomputed from this post.
    bool RequiresAuditReason,
    string? AuditWhy = null);

public sealed record IntendedResultsTestRequest(
    Guid TestId,
    IReadOnlyList<IntendedResultsResultItemRequest> ResultItems,
    IReadOnlyList<IntendedResultsMethodItemRequest> MethodItems,
    IReadOnlyList<IntendedResultsSampleRowRequest> Samples);

public sealed record IntendedResultsResultItemRequest(Guid TestResultItemId, bool IsRange);

public sealed record IntendedResultsMethodItemRequest(Guid TestMethodItemId, string IntendedResult);

public sealed record IntendedResultsSampleRowRequest(
    Guid SampleId,
    bool IsApplicable,
    IReadOnlyList<IntendedResultsSampleResultValueRequest> Values);

public sealed record IntendedResultsSampleResultValueRequest(
    Guid SampleResultItemId, string IntendedResult, string IntendedResult2);

// Legacy validates the whole page as one unit - if anything fails, nothing is saved, so the error
// collections are only ever populated when Success is false. Keyed by the TestResultItemId/
// TestMethodItemId/SampleResultItemId the error belongs to. AuditReasonRequired mirrors legacy's
// unconditional Audit.aspx diversion.
public sealed record IntendedResultsSaveResult(
    bool Success,
    IReadOnlyDictionary<Guid, IntendedResultsFieldError[]> FieldErrorsByItemId,
    bool AuditReasonRequired = false,
    string? AuditObjectName = null);

public sealed record IntendedResultsFieldError(string Field, string Message);
