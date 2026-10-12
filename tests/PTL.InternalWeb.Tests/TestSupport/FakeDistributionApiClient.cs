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

    public SampleNumbersResponse? SampleNumbers { get; set; }
    public IReadOnlyList<string> SampleNumbersErrors { get; set; } = [];
    public SampleNumbersSaveResult? SampleNumbersSaveResult { get; set; }
    public List<SampleNumbersSaveRequest> SavedSampleNumbers { get; } = [];

    public Task<SampleNumbersPageResult> GetSampleNumbersAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SampleNumbersPageResult(SampleNumbers, SampleNumbersErrors));

    public Task<SampleNumbersSaveResult?> SaveSampleNumbersAsync(
        Guid monthlyDistributionSchemeId, SampleNumbersSaveRequest request, CancellationToken cancellationToken = default)
    {
        SavedSampleNumbers.Add(request);
        return Task.FromResult(SampleNumbersSaveResult);
    }

    public IntendedResultsResponse? IntendedResults { get; set; }
    public IntendedResultsSaveResult? IntendedResultsSaveResult { get; set; }
    public List<IntendedResultsSaveRequest> SavedIntendedResults { get; } = [];

    public Task<IntendedResultsResponse?> GetIntendedResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(IntendedResults);

    public Task<IntendedResultsSaveResult?> SaveIntendedResultsAsync(
        Guid monthlyDistributionSchemeId, IntendedResultsSaveRequest request, CancellationToken cancellationToken = default)
    {
        SavedIntendedResults.Add(request);
        return Task.FromResult(IntendedResultsSaveResult);
    }
}
