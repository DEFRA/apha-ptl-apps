namespace PTL.Contracts.TestType;

// Posted to create (POST /api/test-types) or edit (PUT /api/test-types/{testTypeId}) a test type -
// matches legacy ButtonAdd_Click / GridViewTestType_RowUpdating. YearId is whichever year is
// selected in the "Select Year to Edit" dropdown, driving the year-split algorithm.
public sealed record TestTypeSaveRequest(string Name, bool NoLongerInUse, int YearId);
