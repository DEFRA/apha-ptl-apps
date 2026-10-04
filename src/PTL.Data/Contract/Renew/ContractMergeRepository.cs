using Dapper;
using PTL.Core.Contract.Renew;
using PTL.Data.Infrastructure;

namespace PTL.Data.Contract.Renew;

public sealed class ContractMergeRepository(IDbConnectionFactory connectionFactory) : IContractMergeRepository
{
    public async Task<ContractMergeData> GetContractMergeInfoAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        using var multi = await connection.QueryMultipleAsync("EXEC dbo.spgContractMerge @CustomerId", new { CustomerId = customerId });

        var contracts = (await multi.ReadAsync<RenewableContractEntity>()).ToList();
        var participantSchemes = (await multi.ReadAsync<RenewableContractItemEntity>()).ToList();

        // Legacy attaches each item to its parent contract, which is where Suffix comes from -
        // spgContractMerge's second result set does not return it.
        var suffixByContractId = contracts.ToDictionary(c => c.ContractId, c => c.Suffix);
        foreach (var item in participantSchemes)
        {
            item.Suffix = suffixByContractId[item.ContractId];
        }

        return new ContractMergeData(contracts, participantSchemes);
    }
}
