using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeDistributionApiClient : IDistributionApiClient
{
    public IReadOnlyList<DistributionDashboardMonthResponse> Months { get; set; } = [];
    public IReadOnlyList<DistributionYearOptionResponse> Years { get; set; } = [];

    public Task<IReadOnlyList<DistributionDashboardMonthResponse>> GetDashboardAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Months);

    public Task<IReadOnlyList<DistributionYearOptionResponse>> GetYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Years);
}
