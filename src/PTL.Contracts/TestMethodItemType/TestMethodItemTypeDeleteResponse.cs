namespace PTL.Contracts.TestMethodItemType;

// Returned by DELETE /api/test-method-item-types/{testMethodItemTypeId}?yearId={yearId} -
// Success is false (with Message populated) both when the item is still referenced by a
// Scheme/Distribution and when it could not be found, matching legacy's dependency-count
// warning message.
public sealed record TestMethodItemTypeDeleteResponse(bool Success, string? Message);
