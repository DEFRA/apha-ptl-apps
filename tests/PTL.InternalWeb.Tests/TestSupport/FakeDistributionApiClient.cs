using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeDistributionApiClient : IDistributionApiClient
{
    public IReadOnlyList<DistributionDashboardMonthResponse> Months { get; set; } = [];
    public IReadOnlyList<DistributionYearOptionResponse> Years { get; set; } = [];
    public MonthlyDistributionResponse? Schedule { get; set; }
    public MonthlyDistributionScheduleSaveResult? SaveResult { get; set; }
    public List<MonthlyDistributionScheduleRowRequest> SavedRows { get; } = [];

    public Task<IReadOnlyList<DistributionDashboardMonthResponse>> GetDashboardAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Months);

    public Task<IReadOnlyList<DistributionYearOptionResponse>> GetYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Years);

    public Task<MonthlyDistributionResponse?> GetScheduleAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Schedule);

    public Task<MonthlyDistributionScheduleSaveResult?> SaveScheduleAsync(
        int yearId, int monthId, IReadOnlyList<MonthlyDistributionScheduleRowRequest> rows, CancellationToken cancellationToken = default)
    {
        SavedRows.AddRange(rows);
        return Task.FromResult(SaveResult);
    }
}
