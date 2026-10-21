namespace PTL.Contracts.CriterionItemType;

// Returned by DELETE /api/criterion-item-types/{criterionItemTypeId}?yearId={yearId} - Success is
// false (with Message populated) both when the item is still referenced by a Scheme/Distribution
// and when it could not be found, matching legacy's dependency-count warning message.
public sealed record CriterionItemTypeDeleteResponse(bool Success, string? Message);
