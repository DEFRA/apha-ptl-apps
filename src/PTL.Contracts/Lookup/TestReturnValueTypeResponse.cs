namespace PTL.Contracts.Lookup;

// Public API contract for GET /api/lookups/test-return-value-types (spgaTestReturnValueType) -
// backs the Return Type dropdown on the SchemeAdmin "Test Result Items" screen.
public sealed record TestReturnValueTypeResponse(Guid TestReturnValueTypeId, string Name);
