using Dapper;
using PTL.Core.Contract.SampleAddress;
using PTL.Data.Infrastructure;

namespace PTL.Data.Contract.SampleAddress;

public sealed class SampleAddressRepository(IDbConnectionFactory connectionFactory) : ISampleAddressRepository
{
    // Legacy trims trailing ", " from the SP's month concatenation (SampleAddressScheme.Fetch).
    private static readonly char[] MonthsActiveTrimChars = [' ', ','];

    public async Task<IReadOnlyList<SampleAddressEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Three result sets: addresses, fee-paying schemes, non-fee-paying schemes.
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgExportSampleAddressesByContractId @ContractId",
            new { ContractId = contractId });

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
