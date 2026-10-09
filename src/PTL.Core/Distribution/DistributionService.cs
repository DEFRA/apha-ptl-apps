namespace PTL.Core.Distribution;

public interface IDistributionService
{
    Task<IReadOnlyList<DistributionDashboardMonth>> GetDashboardAsync(int financialYearId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionYearOption>> GetYearOptionsAsync(CancellationToken cancellationToken = default);
}

public sealed class DistributionService(IDistributionRepository distributionRepository) : IDistributionService
{
    public async Task<IReadOnlyList<DistributionDashboardMonth>> GetDashboardAsync(int financialYearId, CancellationToken cancellationToken = default)
    {
        var allMonths = await distributionRepository.GetMonthlyDistributionSummariesAsync(cancellationToken);
        return DistributionDashboardBuilder.Build(allMonths, financialYearId);
    }

    public async Task<IReadOnlyList<DistributionYearOption>> GetYearOptionsAsync(CancellationToken cancellationToken = default)
    {
        var distributionYears = await distributionRepository.GetDistributionYearsAsync(cancellationToken);
        return DistributionYearOptionsBuilder.Build(distributionYears, DateTime.UtcNow);
    }
}
