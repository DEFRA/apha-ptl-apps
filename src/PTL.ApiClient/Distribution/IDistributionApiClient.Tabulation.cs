using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public partial interface IDistributionApiClient
{
    Task<TabulationReportResponse?> GetTabulationAsync(Guid tabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default);

    Task<PublishedTabulationsResponse?> GetPublishedTabulationsAsync(Guid tabulationId, CancellationToken cancellationToken = default);

    Task<TabulationReportResponse?> GetPublishedTabulationAsync(Guid publishedTabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default);

    Task<AssessmentResultsResponse?> GetAssessmentResultsAsync(Guid tabulationId, Guid testId, bool isPublished, CancellationToken cancellationToken = default);

    Task<TabulationAttachmentFile?> GetTabulationAttachmentAsync(Guid tabulationId, bool isPublished, Guid attachmentId, CancellationToken cancellationToken = default);

    // ExternalWeb-only: scoped to the caller's own Participant/Viewer/Test Consultant access (see
    // DistributionController.Tabulation.cs's "for-user" endpoints / ParticipantTabulationService).
    Task<PublishedTabulationSummaryListResponse> GetTabulationsForUserAsync(
        Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default);

    Task<TabulationReportResponse?> GetPublishedTabulationForUserAsync(
        Guid publishedTabulationId, Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default);

    Task<AssessmentResultsResponse?> GetAssessmentResultsForUserAsync(
        Guid publishedTabulationId, Guid testId, Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default);

    Task<TabulationAttachmentFile?> GetPublishedTabulationAttachmentForUserAsync(
        Guid publishedTabulationId, Guid attachmentId, Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default);
}

// ContentDisposition is passed through unchanged so the API decides inline vs download.
public sealed record TabulationAttachmentFile(byte[] Content, string ContentType, string ContentDisposition);
