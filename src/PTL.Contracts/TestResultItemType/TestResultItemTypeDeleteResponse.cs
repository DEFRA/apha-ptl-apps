namespace PTL.Contracts.TestResultItemType;

// Returned by DELETE /api/test-result-item-types/{testResultItemTypeId}?yearId={yearId} -
// Success is false (with Message populated) both when the item is still referenced by a
// Scheme/Distribution and when it could not be found, matching legacy's dependency-count
// warning message.
public sealed record TestResultItemTypeDeleteResponse(bool Success, string? Message);
