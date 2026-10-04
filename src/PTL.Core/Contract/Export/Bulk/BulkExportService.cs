using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;

namespace PTL.Core.Contract.Export.Bulk;

/// <summary>
/// The whole-dataset reads behind the four Exports screens. Legacy loads each dataset in a single
/// call and mail merges the lot into one document, so these mirror that shape rather than looping
/// the per-contract endpoints.
/// </summary>
public interface IBulkExportRepository
{
    // spgaExportContractDetails - two result sets, contracts then their fee-paying items.
    Task<IReadOnlyList<BulkContractEntity>> GetContractsAsync(CancellationToken cancellationToken = default);

    // spgaExportSampleAddress - current contract year only, three result sets.
    Task<IReadOnlyList<SampleAddressEntity>> GetSampleAddressesAsync(CancellationToken cancellationToken = default);

    // spgExportContractRenewal @NonUk
    Task<IReadOnlyList<ContractRenewalEntity>> GetRenewalsAsync(bool nonUk, CancellationToken cancellationToken = default);
}

public interface IBulkExportService
{
    /// <summary>Contracts and Job Sheets both export every contract whose total is non-zero.</summary>
    Task<IReadOnlyList<BulkContractEntity>> GetExportableContractsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SampleAddressEntity>> GetSampleAddressesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContractRenewalEntity>> GetRenewalsAsync(bool nonUk, CancellationToken cancellationToken = default);
}

public sealed class BulkExportService(IBulkExportRepository repository) : IBulkExportService
{
    public async Task<IReadOnlyList<BulkContractEntity>> GetExportableContractsAsync(CancellationToken cancellationToken = default)
    {
        var contracts = await repository.GetContractsAsync(cancellationToken);

        // Legacy MailMergeContract/MailMergeJobSheet: "Don't export contracts with a Grand Total of 0".
        return [.. contracts.Where(c => c.TotalPrice != 0m)];
    }

    public Task<IReadOnlyList<SampleAddressEntity>> GetSampleAddressesAsync(CancellationToken cancellationToken = default) =>
        repository.GetSampleAddressesAsync(cancellationToken);

    public Task<IReadOnlyList<ContractRenewalEntity>> GetRenewalsAsync(bool nonUk, CancellationToken cancellationToken = default) =>
        repository.GetRenewalsAsync(nonUk, cancellationToken);
}
