namespace PTL.Core.Customer;

public sealed record CustomerSearchResult(IReadOnlyList<CustomerSummaryEntity> Items, int TotalCount);
