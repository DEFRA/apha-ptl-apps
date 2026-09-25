using Dapper;
using PTL.Core.Contract.ImportPermit;
using PTL.Data.Infrastructure;

namespace PTL.Data.Contract.ImportPermit;

public sealed class ImportPermitRepository(IDbConnectionFactory connectionFactory) : IImportPermitRepository
{
    public async Task<IReadOnlyList<ImportPermitEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<ImportPermitEntity>(
            "EXEC dbo.spgImportPermitDetailsByContractId @ContractId",
            new { ContractId = contractId })).ToList();
    }

    public async Task UpdateAsync(Guid participantSchemeId, bool importPermitReceived, DateTime? importPermitExpiry, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuUpdateImportPermit @ParticipantSchemeId, @ImportPermitReceived, @ImportPermitExpiry",
            new { ParticipantSchemeId = participantSchemeId, ImportPermitReceived = importPermitReceived, ImportPermitExpiry = importPermitExpiry });
    }
}
