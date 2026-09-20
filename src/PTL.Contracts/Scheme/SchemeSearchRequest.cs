namespace PTL.Contracts.Scheme;

// Query-binding contract for GET /api/schemes. Mirrors ContractSearchRequest's shape: yearId is
// the only server-enforced filter (spgSchemeInfoByYearId has no search/paging concept); search
// term and paging are applied in-memory over the year's results (matches SchemeList.aspx, which
// has no year-independent "all schemes" search screen in the legacy UI).
public sealed record SchemeSearchRequest(int YearId, string? SearchTerm = null, int Page = 1, int PageSize = 20);
