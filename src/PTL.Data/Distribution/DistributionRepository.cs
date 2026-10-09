using Dapper;
using PTL.Core.Distribution;
using PTL.Data.Infrastructure;

namespace PTL.Data.Distribution;

public sealed class DistributionRepository(IDbConnectionFactory connectionFactory) : IDistributionRepository
{
    public async Task<IReadOnlyList<DistributionMonthSummaryEntity>> GetMonthlyDistributionSummariesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<DistributionMonthSummaryEntity>("EXEC dbo.spgaMonthlyDistributionInfo")).ToList();
    }

    public async Task<IReadOnlyList<DistributionMonthYearEntity>> GetDistributionYearsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<DistributionMonthYearEntity>("EXEC dbo.spgaDistributionYearInfo")).ToList();
    }
}
