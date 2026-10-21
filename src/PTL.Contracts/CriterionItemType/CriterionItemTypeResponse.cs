namespace PTL.Contracts.CriterionItemType;

// Public API contract for the Criteria Items admin screen (GET/POST/PUT/PATCH/DELETE
// /api/criterion-item-types) - matches the legacy CriterionItemTypeList.aspx grid (Name, No
// Longer in Use, Actions) plus the year-scoping and ForeignInstances usage-guard fields the grid
// also reads from spgCriterionItemTypeByYearId. No SharedId - unlike Test Types/Result Items/
// Method Items, Criteria Items do not carry one (confirmed: no fldSharedId column on
// tblCriterionItemType).
public sealed record CriterionItemTypeResponse(
    Guid CriterionItemTypeId,
    string Name,
    int StartYearId,
    int EndYearId,
    bool NoLongerInUse,
    int ForeignInstances,
    string ForeignInstancesString);
