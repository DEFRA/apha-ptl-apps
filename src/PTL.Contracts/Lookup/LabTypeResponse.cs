namespace PTL.Contracts.Lookup;

// Public API contract for GET /api/lookups/lab-types; matches the legacy DropDownLabType binding
// (SystemObjects.LabTypeCollection / spgaLabType) on Participant.aspx.
public sealed record LabTypeResponse(Guid LabTypeId, string Name);
