namespace PTL.Core.Distribution;

public interface IDistributionRepository
{
    Task<IReadOnlyList<DistributionMonthSummaryEntity>> GetMonthlyDistributionSummariesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionMonthYearEntity>> GetDistributionYearsAsync(CancellationToken cancellationToken = default);
}
