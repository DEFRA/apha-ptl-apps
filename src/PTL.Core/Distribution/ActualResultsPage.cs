namespace PTL.Core.Distribution;

// Actual Results screen (legacy Internal User/ActualResults.aspx): one table per Test that has at
// least one applicable Sample, Result Items as columns, applicable Samples as rows, Method Items as
// test-level rows. IsNew = no results were ever saved (legacy FirstEditDate is empty), which
// decides whether Save needs the Audit.aspx reason step.
public sealed record ActualResultsPage(
    Guid DistributionParticipantId,
    Guid MonthlyDistributionSchemeId,
    string Heading,
    string AuditObjectName,
    DateTime DeadlineDate,
    bool IsNew,
    IReadOnlyList<ActualResultsTest> Tests,
    bool IsReadOnly = false);

public sealed record ActualResultsTest(
    Guid TestId,
    string TestName,
    IReadOnlyList<ActualResultsResultItem> ResultItems,
    IReadOnlyList<ActualResultsMethodItem> MethodItems,
    IReadOnlyList<ActualResultsSampleRow> Samples);

public sealed record ActualResultsResultItem(Guid TestResultItemId, string Name, string FormatString, string ReturnValueType);

public sealed record ActualResultsMethodItem(Guid TestMethodItemId, string Name, string FormatString, string ReturnValueType, string Result);

public sealed record ActualResultsSampleRow(Guid SampleId, string SampleNumber, IReadOnlyList<ActualResultsSampleValue> Values);

public sealed record ActualResultsSampleValue(Guid SampleResultItemId, Guid TestResultItemId, string Result);

// The posted form - only the controls legacy rendered (applicable samples) are present.
public sealed record ActualResultsUpdate(
    IReadOnlyList<ActualResultsSampleValueUpdate> SampleValues,
    IReadOnlyList<ActualResultsMethodItemUpdate> MethodItems,
    string AuditWho,
    string AuditWhy);

public sealed record ActualResultsSampleValueUpdate(Guid SampleResultItemId, string Result);

public sealed record ActualResultsMethodItemUpdate(Guid TestMethodItemId, string Result);

public enum ActualResultsSaveStatus
{
    Saved,
    NothingToSave,
    NotFound,
    ValidationFailed,
    DeadlinePassed,
    AuditReasonRequired,
}

// FieldErrorsByItemId is keyed by SampleResultItemId / TestMethodItemId.
public sealed record ActualResultsSaveOutcome(
    ActualResultsSaveStatus Status,
    IReadOnlyDictionary<Guid, string> FieldErrorsByItemId,
    string? AuditObjectName = null)
{
    private static readonly Dictionary<Guid, string> NoErrors = [];

    public bool Success => Status is ActualResultsSaveStatus.Saved or ActualResultsSaveStatus.NothingToSave;

    public static ActualResultsSaveOutcome Of(ActualResultsSaveStatus status) => new(status, NoErrors);
}
