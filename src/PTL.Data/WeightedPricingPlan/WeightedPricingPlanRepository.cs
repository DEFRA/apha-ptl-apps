using Dapper;
using PTL.Core.Lookup;
using PTL.Core.WeightedPricingPlan;
using PTL.Data.Infrastructure;

namespace PTL.Data.WeightedPricingPlan;

// The stored procedures are unchanged legacy objects (spgaWeightedPricingYear,
// spgPricingPercentageByYearId, sppRenewPricingPlan) - this only adds a Dapper-based access path.
public sealed class WeightedPricingPlanRepository(IDbConnectionFactory connectionFactory) : IWeightedPricingPlanRepository
{
    public async Task<IReadOnlyList<YearEntity>> GetYearsWithPercentagesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<YearEntity>("EXEC dbo.spgaWeightedPricingYear")).ToList();
    }

    public async Task<IReadOnlyList<PricingPercentageEntity>> GetPercentagesForYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<PricingPercentageEntity>(
            "EXEC dbo.spgPricingPercentageByYearId @YearId",
            new { YearId = yearId })).ToList();
    }

    public async Task RenewAsync(int newYearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync("EXEC dbo.sppRenewPricingPlan @YearId", new { YearId = newYearId });
    }
}
