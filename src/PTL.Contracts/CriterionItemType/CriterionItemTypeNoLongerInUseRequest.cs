namespace PTL.Contracts.CriterionItemType;

// Posted to PATCH /api/criterion-item-types/{criterionItemTypeId}/no-longer-in-use - matches
// legacy ButtonSave_Click's per-row loop, which lets "No Longer in Use" be toggled even on a row
// that is otherwise blocked from editing because it is still in use.
public sealed record CriterionItemTypeNoLongerInUseRequest(bool NoLongerInUse, int YearId);
