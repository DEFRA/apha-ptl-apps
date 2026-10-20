using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public partial interface IDistributionApiClient
{
    Task<TabulationReportResponse?> GetTabulationAsync(Guid tabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default);

    Task<PublishedTabulationsResponse?> GetPublishedTabulationsAsync(Guid tabulationId, CancellationToken cancellationToken = default);

    Task<TabulationReportResponse?> GetPublishedTabulationAsync(Guid publishedTabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default);

    Task<AssessmentResultsResponse?> GetAssessmentResultsAsync(Guid tabulationId, Guid testId, bool isPublished, CancellationToken cancellationToken = default);

    Task<TabulationAttachmentFile?> GetTabulationAttachmentAsync(Guid tabulationId, bool isPublished, Guid attachmentId, CancellationToken cancellationToken = default);
}

// ContentDisposition is passed through unchanged so the API decides inline vs download.
public sealed record TabulationAttachmentFile(byte[] Content, string ContentType, string ContentDisposition);
