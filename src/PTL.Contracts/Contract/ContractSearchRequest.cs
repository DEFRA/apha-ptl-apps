namespace PTL.Contracts.Contract;

// Query-binding contract for GET /api/customers/{customerId}/contracts. When YearId is supplied
// the API uses spgContractInfoByCustomerIdAndYearId (exact match, no search/paging applied,
// mirroring legacy behaviour); otherwise it uses spgContractInfoByCustomerId(Period) with
// in-memory search/paging - see ContractService.
public sealed record ContractSearchRequest(int? YearId = null, ContractPeriodFilter Period = ContractPeriodFilter.CurrentAndNext, string? SearchTerm = null, int Page = 1, int PageSize = 20);
