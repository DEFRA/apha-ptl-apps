using PTL.Contracts.Distribution;
using PTL.Core.Participant;
using PTL.Core.TestConsultant;
using PTL.Core.Viewer;

namespace PTL.Core.Distribution;

// Legacy ExternalWeb Tabulations.aspx / TabulationView.aspx / AssessmentResults.aspx - kept out of
// TabulationReportService (whose constructor is shared by InternalWeb features and two existing
// test files) since this is the only consumer that needs
// IParticipantRepository/IViewerRepository/ITestConsultantRepository.
//
// spgPublishedTabulation/spgPublishedAssessmentScoreByTestId only filter by id - they do not check
// the caller is actually authorised to see that tabulation (unlike spgTabulations, which already
// encodes the full Participant/Viewer/TestConsultant + AvailableToParticipants/Viewers rule). So
// every per-id lookup here first re-confirms the id is present in that same user's
// spgTabulations-backed list before calling through to ITabulationReportService, closing that gap
// without any new SQL or stored procedure.
public interface IParticipantTabulationService
{
    Task<PublishedTabulationSummaryListResponse> GetTabulationsForUserAsync(
        Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default);

    Task<TabulationReportResponse?> GetPublishedTabulationForUserAsync(
        Guid publishedTabulationId, Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default);

    Task<AssessmentResultsResponse?> GetAssessmentResultsForUserAsync(
        Guid publishedTabulationId, Guid testId, Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default);

    Task<TabulationSourceAttachment?> GetAttachmentForUserAsync(
        Guid publishedTabulationId, Guid attachmentId, Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default);
}

public sealed class ParticipantTabulationService(
    IDistributionRepository distributionRepository,
    IParticipantRepository participantRepository,
    IViewerRepository viewerRepository,
    ITestConsultantRepository testConsultantRepository,
    ITabulationReportService tabulationReportService) : IParticipantTabulationService
{
    public async Task<PublishedTabulationSummaryListResponse> GetTabulationsForUserAsync(
        Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default)
    {
        var ssoId = await ResolveSsoIdAsync(participantId, viewerId, testConsultantId, cancellationToken);
        if (ssoId is null)
        {
            return new PublishedTabulationSummaryListResponse([]);
        }

        var tabulations = await distributionRepository.GetTabulationsForSsoIdAsync(ssoId.Value, cancellationToken);
        return new PublishedTabulationSummaryListResponse([.. tabulations.Select(ToResponse)]);
    }

    public async Task<TabulationReportResponse?> GetPublishedTabulationForUserAsync(
        Guid publishedTabulationId, Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthorizedAsync(publishedTabulationId, participantId, viewerId, testConsultantId, cancellationToken))
        {
            return null;
        }

        return await tabulationReportService.GetPublishedTabulationAsync(
            publishedTabulationId, participantId ?? Guid.Empty, viewerId ?? Guid.Empty, cancellationToken);
    }

    public async Task<AssessmentResultsResponse?> GetAssessmentResultsForUserAsync(
        Guid publishedTabulationId, Guid testId, Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthorizedAsync(publishedTabulationId, participantId, viewerId, testConsultantId, cancellationToken))
        {
            return null;
        }

        return await tabulationReportService.GetAssessmentResultsAsync(testId, publishedTabulationId, isPublished: true, cancellationToken);
    }

    public async Task<TabulationSourceAttachment?> GetAttachmentForUserAsync(
        Guid publishedTabulationId, Guid attachmentId, Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthorizedAsync(publishedTabulationId, participantId, viewerId, testConsultantId, cancellationToken))
        {
            return null;
        }

        return await tabulationReportService.GetAttachmentAsync(publishedTabulationId, isPublished: true, attachmentId, cancellationToken);
    }

    private async Task<bool> IsAuthorizedAsync(
        Guid publishedTabulationId, Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken)
    {
        var ssoId = await ResolveSsoIdAsync(participantId, viewerId, testConsultantId, cancellationToken);
        if (ssoId is null)
        {
            return false;
        }

        var tabulations = await distributionRepository.GetTabulationsForSsoIdAsync(ssoId.Value, cancellationToken);
        return tabulations.Any(t => t.TabulationId == publishedTabulationId);
    }

    // A person's SsoId is the same across their Participant/Viewer/Test Consultant records (see
    // spgTabulations, which matches Participant, Viewer and Test Consultant rows against the one
    // @SsoId) - resolving via whichever role claim is present is enough to call spgTabulations
    // correctly either way.
    private async Task<Guid?> ResolveSsoIdAsync(Guid? participantId, Guid? viewerId, Guid? testConsultantId, CancellationToken cancellationToken)
    {
        if (participantId is { } pid)
        {
            var participant = await participantRepository.GetByIdAsync(pid, cancellationToken);
            if (participant is not null)
            {
                return participant.SsoId;
            }
        }

        if (viewerId is { } vid)
        {
            // No single-record "viewer by id" stored procedure exists - spgaViewers (GetAllAsync)
            // already returns every viewer including SsoId, so the existing
            // SystemAdministrationController.ViewerManagement/ViewerService.DeleteAsync pattern of
            // fetch-all-then-filter is reused here rather than adding a new query.
            var viewers = await viewerRepository.GetAllAsync(cancellationToken);
            var viewer = viewers.FirstOrDefault(v => v.ViewerId == vid);
            if (viewer is not null)
            {
                return viewer.SsoId;
            }
        }

        if (testConsultantId is { } tcId)
        {
            // Same reasoning as Viewer above - no single-record lookup exists, spgaExtTestConsultants
            // (GetAllAsync, the External Test Consultant Management grid) already returns every test
            // consultant including SsoId.
            var testConsultants = await testConsultantRepository.GetAllAsync(cancellationToken);
            var testConsultant = testConsultants.FirstOrDefault(t => t.ExternalTestConsultantId == tcId);
            if (testConsultant is not null)
            {
                return testConsultant.SsoId;
            }
        }

        return null;
    }

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
