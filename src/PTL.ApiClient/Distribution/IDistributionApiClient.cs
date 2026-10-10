using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public interface IDistributionApiClient
{
    Task<IReadOnlyList<DistributionDashboardMonthResponse>> GetDashboardAsync(int yearId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionYearOptionResponse>> GetYearsAsync(CancellationToken cancellationToken = default);

    Task<MonthlyDistributionResponse?> GetScheduleAsync(int yearId, int monthId, CancellationToken cancellationToken = default);

    Task<MonthlyDistributionScheduleSaveResult?> SaveScheduleAsync(
        int yearId, int monthId, IReadOnlyList<MonthlyDistributionScheduleRowRequest> rows, CancellationToken cancellationToken = default);
}
