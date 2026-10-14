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

    public DistributionParticipantsResponse? Participants { get; set; }
    public ChangeDeadlineResult? ChangeDeadlineResult { get; set; }
    public RepeatDistributionResult? RepeatDistributionResult { get; set; }
    public List<(Guid DistributionParticipantId, ChangeDeadlineRequest Request)> SavedDeadlines { get; } = [];
    public List<RepeatDistributionRequest> SavedRepeatRequests { get; } = [];

    public Task<DistributionParticipantsResponse?> GetParticipantsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Participants);

    public Task<ChangeDeadlineResult?> ChangeDeadlineAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, ChangeDeadlineRequest request, CancellationToken cancellationToken = default)
    {
        SavedDeadlines.Add((distributionParticipantId, request));
        return Task.FromResult(ChangeDeadlineResult);
    }

    public Task<RepeatDistributionResult?> RepeatDistributionAsync(
        Guid monthlyDistributionSchemeId, RepeatDistributionRequest request, CancellationToken cancellationToken = default)
    {
        SavedRepeatRequests.Add(request);
        return Task.FromResult(RepeatDistributionResult);
    }

    public PreparationSheetResponse? Preparation { get; set; }
    public PreparationSaveResult? PreparationSaveResult { get; set; }
    public List<PreparationSaveRequest> SavedPreparationRequests { get; } = [];

    public Task<PreparationSheetResponse?> GetPreparationAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Preparation);

    public Task<PreparationSaveResult?> SavePreparationAsync(
        int yearId, int monthId, PreparationSaveRequest request, CancellationToken cancellationToken = default)
    {
        SavedPreparationRequests.Add(request);
        return Task.FromResult(PreparationSaveResult);
    }

    public ResultsReceiptResponse? ResultsReceipt { get; set; }
    public ResultsReceiptSaveResult? ResultsReceiptSaveResult { get; set; }
    public List<ResultsReceiptSaveRequest> SavedResultsReceiptRequests { get; } = [];

    public Task<ResultsReceiptResponse?> GetResultsReceiptAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultsReceipt);

    public Task<ResultsReceiptSaveResult?> SaveResultsReceiptAsync(
        int yearId, int monthId, ResultsReceiptSaveRequest request, CancellationToken cancellationToken = default)
    {
        SavedResultsReceiptRequests.Add(request);
        return Task.FromResult(ResultsReceiptSaveResult);
    }

    public ActualResultsResponse? ActualResults { get; set; }
    public ActualResultsSaveResult? ActualResultsSaveResult { get; set; }
    public List<ActualResultsSaveRequest> SavedActualResultsRequests { get; } = [];

    public Task<ActualResultsResponse?> GetActualResultsAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ActualResults);

    public Task<ActualResultsSaveResult?> SaveActualResultsAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, ActualResultsSaveRequest request, CancellationToken cancellationToken = default)
    {
        SavedActualResultsRequests.Add(request);
        return Task.FromResult(ActualResultsSaveResult);
    }

    public PackagingSheetResponse? Packaging { get; set; }
    public PackagingSaveResult? PackagingSaveResult { get; set; }
    public PackagingLabelsResponse? PackagingLabels { get; set; }
    public PackagingCustomsResult PackagingCustoms { get; set; } = new(null, string.Empty, "Not configured");
    public List<(int YearId, int MonthId, int Mode, int Week)> PackagingRequests { get; } = [];
    public List<PackagingSaveRequest> SavedPackagingRequests { get; } = [];
    public List<PackagingSelectionRequest> PackagingLabelRequests { get; } = [];
    public List<PackagingSelectionRequest> PackagingCustomsRequests { get; } = [];

    public Task<PackagingSheetResponse?> GetPackagingAsync(int yearId, int monthId, int mode, int week, CancellationToken cancellationToken = default)
    {
        PackagingRequests.Add((yearId, monthId, mode, week));
        return Task.FromResult(Packaging);
    }

    public Task<PackagingSaveResult?> SavePackagingAsync(
        int yearId, int monthId, PackagingSaveRequest request, CancellationToken cancellationToken = default)
    {
        SavedPackagingRequests.Add(request);
        return Task.FromResult(PackagingSaveResult);
    }

    public Task<PackagingLabelsResponse?> GetPackagingLabelsAsync(
        int yearId, int monthId, PackagingSelectionRequest request, CancellationToken cancellationToken = default)
    {
        PackagingLabelRequests.Add(request);
        return Task.FromResult(PackagingLabels);
    }

    public Task<PackagingCustomsResult> GetPackagingCustomsAsync(
        int yearId, int monthId, PackagingSelectionRequest request, CancellationToken cancellationToken = default)
    {
        PackagingCustomsRequests.Add(request);
        return Task.FromResult(PackagingCustoms);
    }
}
