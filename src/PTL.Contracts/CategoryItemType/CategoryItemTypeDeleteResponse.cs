namespace PTL.Contracts.CategoryItemType;

// Returned by DELETE /api/category-item-types/{categoryItemTypeId} - Success is false (with
// Message populated) both when the item is still referenced by a Scheme/Distribution and when it
// could not be found, matching legacy's dependency-count warning message.
public sealed record CategoryItemTypeDeleteResponse(bool Success, string? Message);
