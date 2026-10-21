namespace PTL.Contracts.TestType;

// Public API contract for the Test Types admin screen (GET/POST/PUT/PATCH/DELETE
// /api/test-types) - matches the legacy TestTypeList.aspx grid (Name, No Longer in Use, Actions)
// plus the year-scoping, SharedId, and ForeignInstances usage-guard fields the grid also reads
// from spgTestTypeByYearId.
public sealed record TestTypeResponse(
    Guid TestTypeId,
    Guid SharedId,
    string Name,
    int StartYearId,
    int EndYearId,
    bool NoLongerInUse,
    int ForeignInstances,
    string ForeignInstancesString);
