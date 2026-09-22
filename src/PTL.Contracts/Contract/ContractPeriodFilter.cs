namespace PTL.Contracts.Contract;

// Mirrors the legacy @Active tinyint parameter used by spgContractInfoByCustomerId (see
// docs/analysis/contract-analysis.md): 0 = historical (YearId < current year), 1 = current/next
// year, anything else = all contracts. Distinct from Contract.IsActive (the contract's own active
// flag) - this filters by *year window*, not by active status.
public enum ContractPeriodFilter
{
    Historical = 0,
    CurrentAndNext = 1,
    All = 2
}
