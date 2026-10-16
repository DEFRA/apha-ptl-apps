using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed partial class FakeDistributionApiClient
{
    public TabulationReportResponse? TabulationReport { get; set; }
    public PublishedTabulationsResponse? PublishedTabulations { get; set; }
    public TabulationReportResponse? PublishedTabulationReport { get; set; }
    public AssessmentResultsResponse? AssessmentResults { get; set; }
    public TabulationAttachmentFile? TabulationAttachment { get; set; }
    public List<(Guid TabulationId, Guid ParticipantId, Guid ViewerId)> RequestedTabulations { get; } = [];
    public List<(Guid TabulationId, Guid TestId, bool IsPublished)> RequestedAssessmentResults { get; } = [];

    public Task<TabulationReportResponse?> GetTabulationAsync(Guid tabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default)
    {
        RequestedTabulations.Add((tabulationId, participantId, viewerId));
        return Task.FromResult(TabulationReport);
    }

    public Task<PublishedTabulationsResponse?> GetPublishedTabulationsAsync(Guid tabulationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PublishedTabulations);

    public Task<TabulationReportResponse?> GetPublishedTabulationAsync(Guid publishedTabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default)
    {
        RequestedTabulations.Add((publishedTabulationId, participantId, viewerId));
        return Task.FromResult(PublishedTabulationReport);
    }

    public Task<AssessmentResultsResponse?> GetAssessmentResultsAsync(Guid tabulationId, Guid testId, bool isPublished, CancellationToken cancellationToken = default)
    {
        RequestedAssessmentResults.Add((tabulationId, testId, isPublished));
        return Task.FromResult(AssessmentResults);
    }

    public Task<TabulationAttachmentFile?> GetTabulationAttachmentAsync(Guid tabulationId, bool isPublished, Guid attachmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(TabulationAttachment);
}
