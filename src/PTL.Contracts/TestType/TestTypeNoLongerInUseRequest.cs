namespace PTL.Contracts.TestType;

// Posted to PATCH /api/test-types/{testTypeId}/no-longer-in-use - matches legacy
// ButtonSave_Click's per-row loop, which lets "No Longer in Use" be toggled even on a row that is
// otherwise blocked from editing because it is still in use (see
// TestTypeService.SetNoLongerInUseAsync remarks).
public sealed record TestTypeNoLongerInUseRequest(bool NoLongerInUse, int YearId);
