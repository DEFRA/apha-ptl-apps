using PTL.Contracts.Distribution;

namespace PTL.Core.Distribution;

// Tabulation, Issued Tabulations, Issued Tabulation and the (Issued) Assessment Results chart -
// legacy Scheduling/Tabulation.aspx, PublishedTabulations.aspx, PublishedTabulation.aspx,
// AssessmentResults.aspx and PublishedAssessmentResults.aspx. All read only.
public interface ITabulationReportService
{
    Task<TabulationReportResponse?> GetTabulationAsync(Guid tabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default);

    Task<TabulationReportResponse?> GetPublishedTabulationAsync(Guid publishedTabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default);

    Task<PublishedTabulationsResponse?> GetPublishedTabulationsAsync(Guid tabulationId, CancellationToken cancellationToken = default);

    Task<AssessmentResultsResponse?> GetAssessmentResultsAsync(Guid testId, Guid tabulationId, bool isPublished, CancellationToken cancellationToken = default);

    // The tabulation's "Supporting Document". Null when it does not belong to that tabulation.
    Task<TabulationSourceAttachment?> GetAttachmentAsync(Guid tabulationId, bool isPublished, Guid attachmentId, CancellationToken cancellationToken = default);
}

public sealed class TabulationReportService(IDistributionRepository distributionRepository) : ITabulationReportService
{
    public async Task<TabulationReportResponse?> GetTabulationAsync(Guid tabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default)
    {
        var source = await distributionRepository.GetTabulationSourceAsync(tabulationId, cancellationToken);
        return source is null ? null : TabulationReportBuilder.Build(source, participantId, viewerId);
    }

    public async Task<TabulationReportResponse?> GetPublishedTabulationAsync(Guid publishedTabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default)
    {
        var source = await distributionRepository.GetPublishedTabulationSourceAsync(publishedTabulationId, cancellationToken);
        return source is null ? null : TabulationReportBuilder.Build(source, participantId, viewerId);
    }

    public async Task<PublishedTabulationsResponse?> GetPublishedTabulationsAsync(Guid tabulationId, CancellationToken cancellationToken = default)
    {
        var source = await distributionRepository.GetPublishedTabulationsAsync(tabulationId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        return new PublishedTabulationsResponse(
            tabulationId,
            $"{source.Distribution} {source.SchemeIdentifier}: {source.SchemeName}",
            source.TabulationName,
            [.. source.Tabulations.Select(t => new PublishedTabulationRowResponse(t.PublishedTabulationId, t.CreationDate, t.Signatory))]);
    }

    public async Task<AssessmentResultsResponse?> GetAssessmentResultsAsync(Guid testId, Guid tabulationId, bool isPublished, CancellationToken cancellationToken = default)
    {
        var source = await distributionRepository.GetAssessmentScoresAsync(testId, tabulationId, isPublished, cancellationToken);
        return source is null ? null : AssessmentResultsBuilder.Build(source);
    }

    public async Task<TabulationSourceAttachment?> GetAttachmentAsync(Guid tabulationId, bool isPublished, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var source = isPublished
            ? await distributionRepository.GetPublishedTabulationSourceAsync(tabulationId, cancellationToken)
            : await distributionRepository.GetTabulationSourceAsync(tabulationId, cancellationToken);

        return source?.Attachments.FirstOrDefault(a => a.AttachmentId == attachmentId);
    }
}
