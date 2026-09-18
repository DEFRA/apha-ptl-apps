namespace PTL.Contracts.Contract;

// Public API contract for GET /api/customers/{customerId}/contracts; matches the legacy
// ContractInfo / spgContractInfoByCustomerId(AndYearId) projection.
public sealed record ContractSummaryResponse(Guid ContractId, Guid CustomerId, int YearId, bool IsActive, string Suffix);
