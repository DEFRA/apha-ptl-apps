using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public interface IDistributionApiClient
{
    Task<IReadOnlyList<DistributionDashboardMonthResponse>> GetDashboardAsync(int yearId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionYearOptionResponse>> GetYearsAsync(CancellationToken cancellationToken = default);

    Task<MonthlyDistributionResponse?> GetScheduleAsync(int yearId, int monthId, CancellationToken cancellationToken = default);

    Task<MonthlyDistributionScheduleSaveResult?> SaveScheduleAsync(
        int yearId, int monthId, IReadOnlyList<MonthlyDistributionScheduleRowRequest> rows, CancellationToken cancellationToken = default);

    Task<SampleNumbersPageResult> GetSampleNumbersAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<SampleNumbersSaveResult?> SaveSampleNumbersAsync(
        Guid monthlyDistributionSchemeId, SampleNumbersSaveRequest request, CancellationToken cancellationToken = default);

    Task<IntendedResultsResponse?> GetIntendedResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<IntendedResultsSaveResult?> SaveIntendedResultsAsync(
        Guid monthlyDistributionSchemeId, IntendedResultsSaveRequest request, CancellationToken cancellationToken = default);

    Task<DistributionParticipantsResponse?> GetParticipantsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<ChangeDeadlineResult?> ChangeDeadlineAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, ChangeDeadlineRequest request, CancellationToken cancellationToken = default);

    Task<RepeatDistributionResult?> RepeatDistributionAsync(
        Guid monthlyDistributionSchemeId, RepeatDistributionRequest request, CancellationToken cancellationToken = default);
}
