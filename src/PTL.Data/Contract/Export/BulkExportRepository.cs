using Dapper;
using PTL.Core.Contract.Export.Bulk;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;
using PTL.Data.Infrastructure;

namespace PTL.Data.Contract.Export;

public sealed class BulkExportRepository(IDbConnectionFactory connectionFactory) : IBulkExportRepository
{
    // Legacy SampleAddressScheme.Fetch trims the SP's trailing ", " from the month concatenation.
    private static readonly char[] MonthsActiveTrimChars = [' ', ','];

    public async Task<IReadOnlyList<BulkContractEntity>> GetContractsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        using var results = await connection.QueryMultipleAsync("EXEC dbo.spgaExportContractDetails");

        var contracts = (await results.ReadAsync<BulkContractEntity>()).ToList();
        var items = (await results.ReadAsync<BulkContractItemEntity>()).ToList();

        var itemsByContract = items.GroupBy(i => i.ContractId).ToDictionary(g => g.Key, g => (IReadOnlyList<BulkContractItemEntity>)[.. g]);
        foreach (var contract in contracts)
        {
            contract.Items = itemsByContract.GetValueOrDefault(contract.ContractId, []);
        }

        return contracts;
    }

    public async Task<IReadOnlyList<SampleAddressEntity>> GetSampleAddressesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Three result sets: addresses, fee-paying schemes, non-fee-paying schemes.
        using var results = await connection.QueryMultipleAsync("EXEC dbo.spgaExportSampleAddress");

        var addresses = (await results.ReadAsync<SampleAddressEntity>()).ToList();
        var feePaying = (await results.ReadAsync<SampleAddressSchemeEntity>()).ToList();
        var nonFeePaying = (await results.ReadAsync<SampleAddressSchemeEntity>()).ToList();

        foreach (var address in addresses)
        {
            address.FeePayingSchemes = SchemesFor(feePaying, address);
            address.NonFeePayingSchemes = SchemesFor(nonFeePaying, address);
        }

        return addresses;
    }

    public async Task<IReadOnlyList<ContractRenewalEntity>> GetRenewalsAsync(bool nonUk, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<ContractRenewalEntity>(
            "EXEC dbo.spgExportContractRenewal @NonUk",
            new { NonUk = nonUk })).ToList();
    }

    private static List<SampleAddressSchemeEntity> SchemesFor(List<SampleAddressSchemeEntity> schemes, SampleAddressEntity address)
    {
        var matches = schemes
            .Where(s => s.ContractId == address.ContractId && s.ParticipantId == address.ParticipantId)
            .ToList();

        foreach (var scheme in matches)
        {
            scheme.MonthsActive = (scheme.MonthsActive ?? string.Empty).TrimEnd(MonthsActiveTrimChars);
        }

        return matches;
    }
}
