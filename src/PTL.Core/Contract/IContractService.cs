using PTL.Contracts.Contract;

namespace PTL.Core.Contract;

public interface IContractService
{
    Task<Contract?> GetContractAsync(Guid contractId, CancellationToken cancellationToken = default);

    // If yearId is supplied, uses spgContractInfoByCustomerIdAndYearId (exact match, no search/paging
    // - mirrors the legacy behaviour). Otherwise uses spgContractInfoByCustomerId(period) with
    // in-memory search-by-suffix and paging applied afterwards (Suffix is the only free-text field
    // on the ContractInfo projection - see docs/analysis/contract-analysis.md).
    Task<ContractSearchResult> SearchContractsAsync(Guid customerId, int? yearId, ContractPeriodFilter period, string? searchTerm, int page, int pageSize, CancellationToken cancellationToken = default);

    // Throws ContractValidationException when business rules are violated.
    Task<Contract> CreateContractAsync(Contract contract, CancellationToken cancellationToken = default);

    // Returns null when contractId does not exist. Throws ContractValidationException when business
    // rules are violated, including when the existing contract IsReadOnly (closed year).
    Task<Contract?> UpdateContractAsync(Guid contractId, Contract updatedFields, CancellationToken cancellationToken = default);
}
