using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public interface IDistributionApiClient
{
    Task<IReadOnlyList<DistributionDashboardMonthResponse>> GetDashboardAsync(int yearId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionYearOptionResponse>> GetYearsAsync(CancellationToken cancellationToken = default);
}
