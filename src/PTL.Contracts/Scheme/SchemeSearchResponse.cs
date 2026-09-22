namespace PTL.Contracts.Scheme;

public sealed record SchemeSearchResponse(IReadOnlyList<SchemeSummaryResponse> Items, int TotalCount, int Page, int PageSize);
