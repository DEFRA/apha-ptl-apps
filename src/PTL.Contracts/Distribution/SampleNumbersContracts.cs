namespace PTL.Contracts.Distribution;

// Sample Numbers screen (legacy Scheduling/SampleNumbers.aspx).
public sealed record SampleNumbersResponse(
    Guid MonthlyDistributionSchemeId,
    string DistributionReferenceFull,
    string SchemeIdentifier,
    string SchemeName,
    int NumberOfSetsRequired,
    string Comments,
    bool RequiresAssessment,
    bool IsReadyForAssessment,
    IReadOnlyList<SampleNumberRowResponse> Samples,
    IReadOnlyList<SampleNumbersTestResponse> Tests);

public sealed record SampleNumberRowResponse(
    Guid SampleId,
    string SampleNumber,
    string InternalDescription,
    string ExternalDescription,
    string TabulationDescription,
    string TestConsultantsDescription,
    IReadOnlyList<SampleAllocationResponse> Allocations);

public sealed record SampleAllocationResponse(Guid SampleTestId, Guid TestId, bool IsApplicable);

// One tab of the "Sample Allocation, Method Items and Result Items for each Test" panel.
public sealed record SampleNumbersTestResponse(
    Guid TestId,
    string TestName,
    IReadOnlyList<string> TestResultItemNames,
    IReadOnlyList<string> TestMethodItemNames,
    IReadOnlyList<string> CategoryItemNames);

// AuditWhy carries the reason captured by legacy Audit.aspx. AuditWho is NOT accepted from the
// caller - the API derives it from the authenticated identity.
public sealed record SampleNumbersSaveRequest(
    string Comments,
    IReadOnlyList<SampleNumberRowRequest> Samples,
    string? AuditWhy = null);

public sealed record SampleNumberRowRequest(
    Guid SampleId,
    string SampleNumber,
    string InternalDescription,
    string ExternalDescription,
    string TabulationDescription,
    string TestConsultantsDescription,
    IReadOnlyList<SampleAllocationRequest> Allocations);

public sealed record SampleAllocationRequest(Guid SampleTestId, bool IsApplicable);

// Legacy validates the whole page as one ValidationGroup - if anything fails, nothing is saved,
// so the error collections are only ever populated when Success is false. AuditReasonRequired is
// the legacy Audit.aspx diversion: the edit is valid but a Tabulation Description changed, so a
// mandatory reason must be captured before the save can proceed.
public sealed record SampleNumbersSaveResult(
    bool Success,
    IReadOnlyList<string> PageErrors,
    IReadOnlyDictionary<Guid, SampleNumbersFieldError[]> FieldErrorsBySampleId,
    bool AuditReasonRequired = false,
    string? AuditObjectName = null);

public sealed record SampleNumbersFieldError(string Field, string Message);

// A GET can fail validation because opening Sample Numbers for a never-defined distribution also
// builds its structure from the Scheme. Page null with no Errors means not found.
public sealed record SampleNumbersPageResult(SampleNumbersResponse? Page, IReadOnlyList<string> Errors);
