namespace PTL.Contracts.TestResultItemType;

// Public API contract for the Test Result Items admin screen (GET/POST/PUT/PATCH/DELETE
// /api/test-result-item-types) - matches the legacy TestResultItemTypeList.aspx grid (Name,
// Return Type, No Longer in Use, Actions) plus the year-scoping, SharedId, and ForeignInstances
// usage-guard fields the grid also reads from spgTestResultItemTypeByYearId. Unlike Test Types,
// each item also carries a required Return Type (tblTestReturnValueType) foreign key.
public sealed record TestResultItemTypeResponse(
    Guid TestResultItemTypeId,
    Guid SharedId,
    string Name,
    Guid TestReturnValueTypeId,
    string TestReturnValueTypeName,
    int StartYearId,
    int EndYearId,
    bool NoLongerInUse,
    int ForeignInstances,
    string ForeignInstancesString);
