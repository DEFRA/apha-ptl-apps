namespace PTL.Contracts.CriterionItemType;

// Posted to create (POST /api/criterion-item-types) or edit (PUT
// /api/criterion-item-types/{criterionItemTypeId}) a criterion item type - matches legacy
// ButtonAdd_Click / GridViewCriterionItemType_RowUpdating. YearId is whichever year is selected in
// the "Select Year to Edit" dropdown, driving the year-split algorithm.
public sealed record CriterionItemTypeSaveRequest(string Name, bool NoLongerInUse, int YearId);
