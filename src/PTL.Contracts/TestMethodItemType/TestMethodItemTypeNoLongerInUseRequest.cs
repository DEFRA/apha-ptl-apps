namespace PTL.Contracts.TestMethodItemType;

// Posted to PATCH /api/test-method-item-types/{testMethodItemTypeId}/no-longer-in-use - matches
// legacy ButtonSave_Click's per-row loop, which lets "No Longer in Use" be toggled even on a row
// that is otherwise blocked from editing because it is still in use (see
// TestMethodItemTypeService.SetNoLongerInUseAsync remarks).
public sealed record TestMethodItemTypeNoLongerInUseRequest(bool NoLongerInUse, int YearId);
