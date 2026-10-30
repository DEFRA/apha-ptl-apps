using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.Contracts.Search;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Only GetParticipantDistributionHistoryAsync and GetActualResultsAsync are exercised by
// ExternalWeb's View Results screens; the rest of IDistributionApiClient is InternalWeb-only.
internal sealed class FakeDistributionApiClient : IDistributionApiClient
{
    public ParticipantPastDistributionListResponse ParticipantHistory { get; set; } = new([]);

    public ActualResultsResponse? ActualResults { get; set; }

    public Task<ParticipantPastDistributionListResponse> GetParticipantDistributionHistoryAsync(
        Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ParticipantHistory);

    public Task<ActualResultsResponse?> GetActualResultsAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ActualResults);

    public Task<ActualResultsResponse?> GetActualResultsForParticipantAsync(
        Guid monthlyDistributionSchemeId, Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ActualResults);

    public Task<IReadOnlyList<DistributionDashboardMonthResponse>> GetDashboardAsync(int yearId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<DistributionYearOptionResponse>> GetYearsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<MonthlyDistributionResponse?> GetScheduleAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<MonthlyDistributionScheduleSaveResult?> SaveScheduleAsync(
        int yearId, int monthId, IReadOnlyList<MonthlyDistributionScheduleRowRequest> rows, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<InitialiseMonthResult?> InitialiseMonthAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SampleNumbersPageResult> GetSampleNumbersAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SampleNumbersSaveResult?> SaveSampleNumbersAsync(
        Guid monthlyDistributionSchemeId, SampleNumbersSaveRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IntendedResultsResponse?> GetIntendedResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IntendedResultsSaveResult?> SaveIntendedResultsAsync(
        Guid monthlyDistributionSchemeId, IntendedResultsSaveRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<DistributionParticipantsResponse?> GetParticipantsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ChangeDeadlineResult?> ChangeDeadlineAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, ChangeDeadlineRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<RepeatDistributionResult?> RepeatDistributionAsync(
        Guid monthlyDistributionSchemeId, RepeatDistributionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PreparationSheetResponse?> GetPreparationAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PreparationSaveResult?> SavePreparationAsync(
        int yearId, int monthId, PreparationSaveRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ResultsReceiptResponse?> GetResultsReceiptAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ResultsReceiptSaveResult?> SaveResultsReceiptAsync(
        int yearId, int monthId, ResultsReceiptSaveRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TabulationsResponse?> GetTabulationsAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SampleReceiptResponse?> GetSampleReceiptAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SampleReceiptSaveResult?> SaveSampleReceiptAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, SampleReceiptSaveRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ActualResultsSaveResult?> SaveActualResultsAsync(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, ActualResultsSaveRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PackagingSheetResponse?> GetPackagingAsync(int yearId, int monthId, int mode, int week, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PackagingSaveResult?> SavePackagingAsync(
        int yearId, int monthId, PackagingSaveRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PackagingLabelsResponse?> GetPackagingLabelsAsync(
        int yearId, int monthId, PackagingSelectionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PackagingCustomsResult> GetPackagingCustomsAsync(
        int yearId, int monthId, PackagingSelectionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TabulationReportResponse?> GetTabulationAsync(Guid tabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PublishedTabulationsResponse?> GetPublishedTabulationsAsync(Guid tabulationId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TabulationReportResponse?> GetPublishedTabulationAsync(Guid publishedTabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AssessmentResultsResponse?> GetAssessmentResultsAsync(Guid tabulationId, Guid testId, bool isPublished, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TabulationAttachmentFile?> GetTabulationAttachmentAsync(Guid tabulationId, bool isPublished, Guid attachmentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TabulationsResponse?> PreviewTabulationsAsync(int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TabulationSendResponse?> StageTabulationSendAsync(int yearId, int monthId, TabulationStageRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> SaveTabulationsAsync(int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AmendResultsResponse?> GetAmendResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AmendResultsSaveResult?> SaveAmendResultsAsync(Guid monthlyDistributionSchemeId, AmendResultsSaveRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TestConsultantCommentsResponse?> GetTestConsultantCommentsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TestConsultantCommentsSaveResult?> SaveTestConsultantCommentsAsync(
        Guid monthlyDistributionSchemeId, TestConsultantCommentsSaveRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CommentsAttachmentStageResult?> StageTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, CommentsAttachmentStageRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DiscardStagedTestConsultantCommentsAttachmentsAsync(
        Guid monthlyDistributionSchemeId, IReadOnlyList<Guid> tokens, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TabulationAttachmentFile?> GetTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, Guid attachmentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TabulationAttachmentFile?> GetStagedTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<DistributionNumberSearchResultResponse>> SearchByDistributionNumberAsync(
        DistributionNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SampleNumberSearchResultResponse>> SearchBySampleNumberAsync(
        SampleNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
