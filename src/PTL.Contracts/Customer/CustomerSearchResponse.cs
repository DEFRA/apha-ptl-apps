namespace PTL.Contracts.Customer;

public sealed record CustomerSearchResponse(IReadOnlyList<CustomerSummaryResponse> Items, int TotalCount, int Page, int PageSize);
