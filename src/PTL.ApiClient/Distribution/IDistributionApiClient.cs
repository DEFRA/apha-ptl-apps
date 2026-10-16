using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public partial interface IDistributionApiClient
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

    Task<PreparationSheetResponse?> GetPreparationAsync(int yearId, int monthId, CancellationToken cancellationToken = default);

    Task<PreparationSaveResult?> SavePreparationAsync(
        int yearId, int monthId, PreparationSaveRequest request, CancellationToken cancellationToken = default);

    // Null when the month has not been initialised.
    Task<ResultsReceiptResponse?> GetResultsReceiptAsync(int yearId, int monthId, CancellationToken cancellationToken = default);

    Task<ResultsReceiptSaveResult?> SaveResultsReceiptAsync(
        int yearId, int monthId, ResultsReceiptSaveRequest request, CancellationToken cancellationToken = default);

    Task<TabulationsResponse?> GetTabulationsAsync(int yearId, int monthId, CancellationToken cancellationToken = default);

    // Null when the participant or scheme is not found.
    Task<SampleReceiptResponse?> GetSampleReceiptAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, CancellationToken cancellationToken = default);

    Task<SampleReceiptSaveResult?> SaveSampleReceiptAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, SampleReceiptSaveRequest request, CancellationToken cancellationToken = default);

    // Null when the participant is not found.
    Task<ActualResultsResponse?> GetActualResultsAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, CancellationToken cancellationToken = default);

    Task<ActualResultsSaveResult?> SaveActualResultsAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, ActualResultsSaveRequest request, CancellationToken cancellationToken = default);

    // Null when the month has not been initialised.
    Task<PackagingSheetResponse?> GetPackagingAsync(int yearId, int monthId, int mode, int week, CancellationToken cancellationToken = default);

    Task<PackagingSaveResult?> SavePackagingAsync(
        int yearId, int monthId, PackagingSaveRequest request, CancellationToken cancellationToken = default);

    Task<PackagingLabelsResponse?> GetPackagingLabelsAsync(
        int yearId, int monthId, PackagingSelectionRequest request, CancellationToken cancellationToken = default);

    Task<PackagingCustomsResult> GetPackagingCustomsAsync(
        int yearId, int monthId, PackagingSelectionRequest request, CancellationToken cancellationToken = default);
}
