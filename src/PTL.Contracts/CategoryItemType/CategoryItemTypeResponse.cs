namespace PTL.Contracts.CategoryItemType;

// Public API contract for the Category Items admin screen (GET/POST/PUT/PATCH/DELETE
// /api/category-item-types) - matches the legacy CategoryItemTypeList.aspx grid (Name, No Longer
// in Use, Actions) plus the year-scoping and ForeignInstances usage-guard fields the grid also
// reads from spgCategoryItemTypeByYearId.
public sealed record CategoryItemTypeResponse(
    Guid CategoryItemTypeId,
    string Name,
    int StartYearId,
    int EndYearId,
    bool NoLongerInUse,
    int ForeignInstances,
    string ForeignInstancesString);
