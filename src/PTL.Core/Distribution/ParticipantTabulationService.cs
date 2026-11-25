using System.Diagnostics;
using Microsoft.Extensions.Logging;
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

public sealed partial class ParticipantTabulationService(
    IDistributionRepository distributionRepository,
    ITabulationReportService tabulationReportService,
    ILogger<ParticipantTabulationService> logger) : IParticipantTabulationService
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
        var authorized = tabulations.Any(t => t.TabulationId == publishedTabulationId);
        if (!authorized)
        {
            LogTabulationNotInCallersList(logger, publishedTabulationId, caller.SsoIdExt, caller.ParticipantId, tabulations.Count);
        }

        return authorized;
    }

    // No contact id means no identity to scope to, so nothing is visible.
    private async Task<IReadOnlyList<PublishedTabulationSummaryEntity>> GetCallersTabulationsAsync(
        ExternalTabulationCaller caller, CancellationToken cancellationToken)
    {
        if (caller.SsoIdExt is not { } ssoIdExt || ssoIdExt == Guid.Empty)
        {
            LogNoContactId(logger, caller.ParticipantId, caller.ViewerId);
            return [];
        }

        var participantId = NullIfEmpty(caller.ParticipantId);
        var started = Stopwatch.GetTimestamp();
        var tabulations = await distributionRepository.GetTabulationsForExternalUserAsync(ssoIdExt, participantId, cancellationToken);
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LogTabulationsFound(logger, tabulations.Count, ssoIdExt, participantId, caller.ViewerId, elapsedMs);
        return tabulations;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "External tabulation lookup has no CIDM contact id (ParticipantId={ParticipantId}, ViewerId={ViewerId}); returning no tabulations.")]
    private static partial void LogNoContactId(ILogger logger, Guid? participantId, Guid? viewerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "spgTabulations returned {Count} tabulations for SsoIdExt={SsoIdExt}, ParticipantId={ParticipantId}, ViewerId={ViewerId} in {ElapsedMs:0.0} ms.")]
    private static partial void LogTabulationsFound(ILogger logger, int count, Guid ssoIdExt, Guid? participantId, Guid? viewerId, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published tabulation {PublishedTabulationId} is not in the caller's {Count} tabulations (SsoIdExt={SsoIdExt}, ParticipantId={ParticipantId}).")]
    private static partial void LogTabulationNotInCallersList(ILogger logger, Guid publishedTabulationId, Guid? ssoIdExt, Guid? participantId, int count);

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
