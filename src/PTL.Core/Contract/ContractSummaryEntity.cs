namespace PTL.Core.Contract;

// Keyless domain projection for the list returned by spgContractInfoByCustomerId /
// spgContractInfoByCustomerIdAndYearId. Field set matches PtaBusinessObjects ContractInfo exactly
// (see docs/analysis/contract-analysis.md).
public class ContractSummaryEntity
{
    public Guid ContractId { get; set; }
    public Guid CustomerId { get; set; }
    public int YearId { get; set; }
    public bool IsActive { get; set; }
    public string Suffix { get; set; } = string.Empty;
}
