namespace PTL.Contracts.TestResultItemType;

// Posted to PATCH /api/test-result-item-types/{testResultItemTypeId}/no-longer-in-use - matches
// legacy ButtonSave_Click's per-row loop, which lets "No Longer in Use" be toggled even on a row
// that is otherwise blocked from editing because it is still in use (see
// TestResultItemTypeService.SetNoLongerInUseAsync remarks).
public sealed record TestResultItemTypeNoLongerInUseRequest(bool NoLongerInUse, int YearId);
