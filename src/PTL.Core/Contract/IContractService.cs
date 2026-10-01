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

    // Aggregated read-model over spgContractItems - see PTL.Core.Contract.ContractItemsAggregate.
    // Returns null when contractId does not exist.
    Task<ContractItemsAggregate?> GetContractItemsAsync(Guid contractId, CancellationToken cancellationToken = default);

    // Removes (soft-deletes) a single participant-scheme line item from a contract's items list.
    // Returns false when the contract or the item does not exist, or the item does not belong to
    // this contract. Throws ContractValidationException when the contract IsReadOnly (closed year).
    Task<bool> RemoveContractItemAsync(Guid contractId, Guid participantSchemeId, CancellationToken cancellationToken = default);
}
