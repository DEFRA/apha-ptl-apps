using PTL.Contracts.Contract;

namespace PTL.Core.Contract;

// Defined in Core (not Data) so ContractService can depend on the abstraction without Core
// referencing Data; PTL.Data.Contract.ContractRepository implements this.
public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken = default);

    // Uses spgContractInfoByCustomerId (@Active tinyint: 0=historical, 1=current/next, else=all).
    Task<IReadOnlyList<ContractSummaryEntity>> GetSummariesAsync(Guid customerId, ContractPeriodFilter period, CancellationToken cancellationToken = default);

    // Uses spgContractInfoByCustomerIdAndYearId (exact year match).
    Task<IReadOnlyList<ContractSummaryEntity>> GetSummariesByYearAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default);

    // Uses spiContract; the returned Contract is re-read via GetByIdAsync so QalNumber/CustomerName/
    // IsReadOnly (all computed by the fetch procedure) are populated.
    Task<Contract> CreateAsync(Contract contract, CancellationToken cancellationToken = default);

    // Uses spuContract; returns null when no row was updated (contract does not exist).
    Task<Contract?> UpdateAsync(Contract contract, CancellationToken cancellationToken = default);
}
