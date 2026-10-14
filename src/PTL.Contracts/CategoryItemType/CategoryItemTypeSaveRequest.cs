namespace PTL.Contracts.CategoryItemType;

// Posted to create (POST /api/category-item-types) or edit (PUT
// /api/category-item-types/{categoryItemTypeId}) a category item type - matches legacy
// ButtonAddCategory_Click / GridViewCategoryItemType_RowUpdating. YearId is whichever year is
// selected in the "Select Year to Edit" dropdown, driving the year-split algorithm.
public sealed record CategoryItemTypeSaveRequest(string Name, bool NoLongerInUse, int YearId);
