using Dapper;
using PTL.Core.Audit;
using PTL.Core.Lookup;
using PTL.Core.PostagePricingPlan;
using PTL.Data.Audit;
using PTL.Data.Infrastructure;

namespace PTL.Data.PostagePricingPlan;

// The stored procedures are unchanged legacy objects (spgaPostagePricingYears, spuPostage,
// sppRenewPostagePricingPlan) - this only adds a Dapper-based access path to them.
public sealed class PostagePricingPlanRepository(IDbConnectionFactory connectionFactory) : IPostagePricingPlanRepository
{
    public async Task<IReadOnlyList<YearEntity>> GetYearsWithPlanAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<YearEntity>("EXEC dbo.spgaPostagePricingYears")).ToList();
    }

    public async Task<PostagePricingPlanEntity?> GetByIdAsync(Guid postageId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<PostagePricingPlanEntity>(
            "EXEC dbo.spgPostageByPostageID @PostageId",
            new { PostageId = postageId });
    }

    public Task SetPriceAsync(Guid postageId, int yearId, decimal ukPrice, decimal euPrice, decimal nonEuPrice, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default) =>
        AuditTrailWriter.ExecuteAsync(
            connectionFactory,
            auditEntry,
            (connection, transaction) => connection.ExecuteAsync(
                "EXEC dbo.spuPostage @fldPostageId, @fldUkPrice, @fldEuPrice, @fldNonEuPrice, @fldYearId",
                new { fldPostageId = postageId, fldUkPrice = ukPrice, fldEuPrice = euPrice, fldNonEuPrice = nonEuPrice, fldYearId = yearId },
                transaction),
            cancellationToken);

    public Task RenewAsync(int newYearId, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default) =>
        AuditTrailWriter.ExecuteAsync(
            connectionFactory,
            auditEntry,
            (connection, transaction) => connection.ExecuteAsync("EXEC dbo.sppRenewPostagePricingPlan @YearId", new { YearId = newYearId }, transaction),
            cancellationToken);
}
