namespace PTL.Core.Contract;

public sealed record ContractSearchResult(IReadOnlyList<ContractSummaryEntity> Items, int TotalCount);
