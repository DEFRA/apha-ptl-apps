using Dapper;
using PTL.Core.Contract.Renewal;
using PTL.Data.Infrastructure;

namespace PTL.Data.Contract.Renewal;

/// <summary>
/// Legacy <c>MailMergeRenewalLetter.ExecuteSingleRenewalLetter</c> fetches the whole renewal
/// collection via the unparameterised <c>spgaExportContractRenewal</c> and then selects by contract
/// id in memory. That behaviour is reproduced exactly; the procedure has no by-id variant.
/// </summary>
public sealed class ContractRenewalRepository(IDbConnectionFactory connectionFactory) : IContractRenewalRepository
{
    public async Task<ContractRenewalEntity?> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var renewals = await connection.QueryAsync<ContractRenewalEntity>("EXEC dbo.spgaExportContractRenewal");

        return renewals.FirstOrDefault(r => r.ContractId == contractId);
    }
}
