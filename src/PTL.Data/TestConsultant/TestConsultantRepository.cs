using Dapper;
using PTL.Core.Audit;
using PTL.Core.TestConsultant;
using PTL.Data.Audit;
using PTL.Data.Infrastructure;

namespace PTL.Data.TestConsultant;

public sealed class TestConsultantRepository(IDbConnectionFactory connectionFactory) : ITestConsultantRepository
{
    public async Task<Core.TestConsultant.TestConsultant?> GetBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<Core.TestConsultant.TestConsultant>(
            "EXEC dbo.spgTestConsultantBySsoId @SsoIdExt=@SsoIdExt",
            new { SsoIdExt = ssoIdExt });
    }

    public async Task<Core.TestConsultant.TestConsultant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<Core.TestConsultant.TestConsultant>(
            "EXEC dbo.spgTestConsultantByEmail @Email",
            new { Email = email });
    }

    public async Task<IReadOnlyList<Core.TestConsultant.TestConsultant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<Core.TestConsultant.TestConsultant>("EXEC dbo.spgaExtTestConsultants")).ToList();
    }

    public Task<Core.TestConsultant.TestConsultant> CreateAsync(Core.TestConsultant.TestConsultant testConsultant, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default) =>
        AuditTrailWriter.ExecuteAsync(connectionFactory, auditEntry, async (connection, transaction) =>
        {
            await connection.ExecuteAsync(
                "EXEC dbo.spiExtTestConsultant @ExternalTestConsultantId, @Name, @Department, @Email, @SsoId, @IsInactive, @InactiveDate, @SsoIdExt",
                new
                {
                    testConsultant.ExternalTestConsultantId,
                    testConsultant.Name,
                    testConsultant.Department,
                    testConsultant.Email,
                    testConsultant.SsoId,
                    testConsultant.IsInactive,
                    testConsultant.InactiveDate,
                    testConsultant.SsoIdExt
                },
                transaction);

            return testConsultant;
        }, cancellationToken);

    public Task<Core.TestConsultant.TestConsultant?> UpdateAsync(Core.TestConsultant.TestConsultant testConsultant, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default) =>
        AuditTrailWriter.ExecuteAsync<Core.TestConsultant.TestConsultant?>(connectionFactory, auditEntry, async (connection, transaction) =>
        {
            var rowsAffected = await connection.ExecuteAsync(
                "EXEC dbo.spuExtTestConsultant @ExternalTestConsultantId, @Name, @Department, @Email, @SsoId, @IsInactive, @InactiveDate, @SsoIdExt",
                new
                {
                    testConsultant.ExternalTestConsultantId,
                    testConsultant.Name,
                    testConsultant.Department,
                    testConsultant.Email,
                    testConsultant.SsoId,
                    testConsultant.IsInactive,
                    testConsultant.InactiveDate,
                    testConsultant.SsoIdExt
                },
                transaction);

            return rowsAffected == 0 ? null : testConsultant;
        }, cancellationToken);
}
