namespace PTL.Contracts.TestMethodItemType;

// Posted to create (POST /api/test-method-item-types) or edit
// (PUT /api/test-method-item-types/{testMethodItemTypeId}) a test method item type - matches
// legacy ButtonAdd_Click / GridViewTestMethodItemType_RowUpdating. YearId is whichever year is
// selected in the "Select Year to Edit" dropdown, driving the year-split algorithm.
public sealed record TestMethodItemTypeSaveRequest(string Name, Guid TestReturnValueTypeId, bool NoLongerInUse, int YearId);
