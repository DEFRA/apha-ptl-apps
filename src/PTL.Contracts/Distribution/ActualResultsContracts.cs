namespace PTL.Contracts.Distribution;

// Actual Results screen (legacy Internal User/ActualResults.aspx) - one table per Test that has at
// least one applicable Sample. IsNew = no results were ever saved for this participant.
public sealed record ActualResultsResponse(
    Guid DistributionParticipantId,
    Guid MonthlyDistributionSchemeId,
    string Heading,
    string AuditObjectName,
    DateTime DeadlineDate,
    bool IsNew,
    IReadOnlyList<ActualResultsTestResponse> Tests,
    bool IsReadOnly = false);

public sealed record ActualResultsTestResponse(
    Guid TestId,
    string TestName,
    IReadOnlyList<ActualResultsResultItemResponse> ResultItems,
    IReadOnlyList<ActualResultsMethodItemResponse> MethodItems,
    IReadOnlyList<ActualResultsSampleRowResponse> Samples);

public sealed record ActualResultsResultItemResponse(Guid TestResultItemId, string Name, string FormatString, string ReturnValueType);

public sealed record ActualResultsMethodItemResponse(Guid TestMethodItemId, string Name, string FormatString, string ReturnValueType, string Result);

public sealed record ActualResultsSampleRowResponse(Guid SampleId, string SampleNumber, IReadOnlyList<ActualResultsSampleValueResponse> Values);

public sealed record ActualResultsSampleValueResponse(Guid SampleResultItemId, Guid TestResultItemId, string Result);

public sealed record ActualResultsSaveRequest(
    IReadOnlyList<ActualResultsSampleValueRequest> SampleValues,
    IReadOnlyList<ActualResultsMethodItemRequest> MethodItems,
    string? AuditWhy = null);

public sealed record ActualResultsSampleValueRequest(Guid SampleResultItemId, string Result);

public sealed record ActualResultsMethodItemRequest(Guid TestMethodItemId, string Result);

// Legacy validates the whole page as one unit - nothing is saved when any value is invalid.
// FieldErrorsByItemId is keyed by SampleResultItemId / TestMethodItemId. DeadlinePassed mirrors
// "Save Failed: No results can be saved after the deadline date."; AuditReasonRequired mirrors
// legacy's Audit.aspx diversion when results already exist.
public sealed record ActualResultsSaveResult(
    bool Success,
    IReadOnlyDictionary<Guid, string> FieldErrorsByItemId,
    bool DeadlinePassed = false,
    bool AuditReasonRequired = false,
    string? AuditObjectName = null);
