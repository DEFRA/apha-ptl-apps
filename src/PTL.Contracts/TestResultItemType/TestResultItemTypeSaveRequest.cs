namespace PTL.Contracts.TestResultItemType;

// Posted to create (POST /api/test-result-item-types) or edit
// (PUT /api/test-result-item-types/{testResultItemTypeId}) a test result item type - matches
// legacy ButtonAdd_Click / GridViewTestResultItemType_RowUpdating. YearId is whichever year is
// selected in the "Select Year to Edit" dropdown, driving the year-split algorithm.
public sealed record TestResultItemTypeSaveRequest(string Name, Guid TestReturnValueTypeId, bool NoLongerInUse, int YearId);
