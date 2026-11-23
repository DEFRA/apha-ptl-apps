using PTL.Contracts.Distribution;

namespace PTL.Core.Distribution;

// Legacy ExternalWeb Tabulations.aspx / TabulationView.aspx / AssessmentResults.aspx. Access is decided by
// spgTabulations' @SsoIdExt branch from the caller's CIDM contact id (and selected lab), never by the legacy fldSsoId,
// which the new application does not maintain. spgPublishedTabulation/spgPublishedAssessmentScoreByTestId only filter
// by id, so every per-id lookup first re-confirms the id is in that same caller's list.
public interface IParticipantTabulationService
{
    Task<PublishedTabulationSummaryListResponse> GetTabulationsForUserAsync(
        ExternalTabulationCaller caller, CancellationToken cancellationToken = default);

    Task<TabulationReportResponse?> GetPublishedTabulationForUserAsync(
        Guid publishedTabulationId, ExternalTabulationCaller caller, CancellationToken cancellationToken = default);

    Task<AssessmentResultsResponse?> GetAssessmentResultsForUserAsync(
        Guid publishedTabulationId, Guid testId, ExternalTabulationCaller caller, CancellationToken cancellationToken = default);

    Task<TabulationSourceAttachment?> GetAttachmentForUserAsync(
        Guid publishedTabulationId, Guid attachmentId, ExternalTabulationCaller caller, CancellationToken cancellationToken = default);
}

public sealed class ParticipantTabulationService(
    IDistributionRepository distributionRepository,
    ITabulationReportService tabulationReportService) : IParticipantTabulationService
{
    public async Task<PublishedTabulationSummaryListResponse> GetTabulationsForUserAsync(
        ExternalTabulationCaller caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var tabulations = await GetCallersTabulationsAsync(caller, cancellationToken);
        return new PublishedTabulationSummaryListResponse([.. tabulations.Select(ToResponse)]);
    }

    public async Task<TabulationReportResponse?> GetPublishedTabulationForUserAsync(
        Guid publishedTabulationId, ExternalTabulationCaller caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!await IsAuthorizedAsync(publishedTabulationId, caller, cancellationToken))
        {
            return null;
        }

        return await tabulationReportService.GetPublishedTabulationAsync(
            publishedTabulationId, caller.ParticipantId ?? Guid.Empty, caller.ViewerId ?? Guid.Empty, cancellationToken);
    }

    public async Task<AssessmentResultsResponse?> GetAssessmentResultsForUserAsync(
        Guid publishedTabulationId, Guid testId, ExternalTabulationCaller caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!await IsAuthorizedAsync(publishedTabulationId, caller, cancellationToken))
        {
            return null;
        }

        return await tabulationReportService.GetAssessmentResultsAsync(testId, publishedTabulationId, isPublished: true, cancellationToken);
    }

    public async Task<TabulationSourceAttachment?> GetAttachmentForUserAsync(
        Guid publishedTabulationId, Guid attachmentId, ExternalTabulationCaller caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!await IsAuthorizedAsync(publishedTabulationId, caller, cancellationToken))
        {
            return null;
        }

        return await tabulationReportService.GetAttachmentAsync(publishedTabulationId, isPublished: true, attachmentId, cancellationToken);
    }

    private async Task<bool> IsAuthorizedAsync(Guid publishedTabulationId, ExternalTabulationCaller caller, CancellationToken cancellationToken)
    {
        var tabulations = await GetCallersTabulationsAsync(caller, cancellationToken);
        return tabulations.Any(t => t.TabulationId == publishedTabulationId);
    }

    // No contact id means no identity to scope to, so nothing is visible.
    private Task<IReadOnlyList<PublishedTabulationSummaryEntity>> GetCallersTabulationsAsync(
        ExternalTabulationCaller caller, CancellationToken cancellationToken) =>
        caller.SsoIdExt is { } ssoIdExt && ssoIdExt != Guid.Empty
            ? distributionRepository.GetTabulationsForExternalUserAsync(ssoIdExt, NullIfEmpty(caller.ParticipantId), cancellationToken)
            : Task.FromResult<IReadOnlyList<PublishedTabulationSummaryEntity>>([]);

    private static Guid? NullIfEmpty(Guid? id) => id == Guid.Empty ? null : id;

    private static PublishedTabulationSummaryResponse ToResponse(PublishedTabulationSummaryEntity tabulation) => new(
        tabulation.TabulationId,
        tabulation.DistributionReference,
        tabulation.SchemeCode,
        tabulation.SchemeNameOnly,
        tabulation.TabulationName,
        tabulation.DistributionDate,
        tabulation.YearId,
        tabulation.MonthId);
}
