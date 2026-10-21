namespace PTL.Contracts.CategoryItemType;

// Posted to PATCH /api/category-item-types/{categoryItemTypeId}/no-longer-in-use - matches
// legacy ButtonSave_Click's per-row loop, which lets "No Longer in Use" be toggled even on a row
// that is otherwise blocked from editing because it is still in use (see
// CategoryItemTypeService.SetNoLongerInUseAsync remarks).
public sealed record CategoryItemTypeNoLongerInUseRequest(bool NoLongerInUse, int YearId);
