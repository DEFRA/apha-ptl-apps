namespace PTL.Contracts.Contract;

public sealed record ContractSearchResponse(IReadOnlyList<ContractSummaryResponse> Items, int TotalCount, int Page, int PageSize);
