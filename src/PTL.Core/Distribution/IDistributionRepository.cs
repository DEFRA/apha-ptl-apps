namespace PTL.Core.Distribution;

public interface IDistributionRepository
{
    Task<IReadOnlyList<DistributionMonthSummaryEntity>> GetMonthlyDistributionSummariesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionMonthYearEntity>> GetDistributionYearsAsync(CancellationToken cancellationToken = default);

    // spgMonthlyDistribution - the Monthly Distributions (Scheduling) screen's data source.
    // Returns null if the month has not been initialised (no tblMonthlyDistribution row yet).
    Task<(Guid MonthlyDistributionId, IReadOnlyList<MonthlyDistributionSchemeEntity> Schemes)?> GetMonthlyDistributionSchedulesAsync(
        int yearId, int monthId, CancellationToken cancellationToken = default);

    // spuMonthlyDistributionScheme x N, in a single transaction - legacy saves the whole page's
    // scheme list as one logical unit (the "Dates" ValidationGroup blocks the postback entirely if
    // any row is invalid, so either every row is saved or none are).
    Task UpdateMonthlyDistributionSchemesAsync(IReadOnlyList<MonthlyDistributionSchemeEntity> schemes, CancellationToken cancellationToken = default);
}
