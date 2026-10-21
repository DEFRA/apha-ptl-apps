namespace PTL.Contracts.TestMethodItemType;

// Public API contract for the Test Method Items admin screen (GET/POST/PUT/PATCH/DELETE
// /api/test-method-item-types) - matches the legacy TestMethodItemTypeList.aspx grid (Name,
// Return Type, No Longer in Use, Actions) plus the year-scoping, SharedId, and ForeignInstances
// usage-guard fields the grid also reads from spgTestMethodItemTypeByYearId. Identical shape to
// TestResultItemType (Slice 4) - shares the same tblTestReturnValueType lookup.
public sealed record TestMethodItemTypeResponse(
    Guid TestMethodItemTypeId,
    Guid SharedId,
    string Name,
    Guid TestReturnValueTypeId,
    string TestReturnValueTypeName,
    int StartYearId,
    int EndYearId,
    bool NoLongerInUse,
    int ForeignInstances,
    string ForeignInstancesString);
