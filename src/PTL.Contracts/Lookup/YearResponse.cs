namespace PTL.Contracts.Lookup;

// Public API contract for GET /api/lookups/years; matches the legacy DropDownYear binding
// (SystemObjects.YearCollection.FetchYearCollectionCurrent / spgaYearCurrent) on Contract.aspx -
// current + next year only.
public sealed record YearResponse(int YearId, string Year);
