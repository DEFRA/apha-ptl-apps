namespace PTL.Core.Contract.Renewal;

// Mirrors legacy Exports.ContractRenewal / spgaExportContractRenewal.
public sealed class ContractRenewalEntity
{
    public Guid ContractId { get; set; }
    public Guid CustomerId { get; set; }
    public string QalNumber { get; set; } = string.Empty;
    public string OrganisationName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Address4 { get; set; } = string.Empty;
    public string Address5 { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTime ContractStartDate { get; set; }
    public DateTime ContractEndDate { get; set; }
    public string RenewalInformation { get; set; } = string.Empty;
}

public interface IContractRenewalRepository
{
    Task<ContractRenewalEntity?> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default);
}

public interface IContractRenewalService
{
    Task<ContractRenewalEntity?> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default);
}

public sealed class ContractRenewalService(IContractRenewalRepository contractRenewalRepository) : IContractRenewalService
{
    public Task<ContractRenewalEntity?> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        contractRenewalRepository.GetByContractIdAsync(contractId, cancellationToken);
}
