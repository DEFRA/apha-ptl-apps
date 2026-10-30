using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// Tabulation, Issued Tabulations, Issued Tabulation and the assessment results chart (legacy
// Scheduling/Tabulation.aspx, PublishedTabulations.aspx, PublishedTabulation.aspx,
// AssessmentResults.aspx, PublishedAssessmentResults.aspx). Read only.
public sealed partial class DistributionController
{
    // Content types the browser may show inline (legacy displayed every attachment inline); anything
    // else is downloaded so stored files cannot run script in the application's origin.
    private static readonly HashSet<string> InlineAttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg", "image/gif",
    };

    [HttpGet("tabulations/{tabulationId:guid}")]
    [ProducesResponseType<TabulationReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TabulationReportResponse>> GetTabulation(
        Guid tabulationId,
        [FromQuery] Guid? participantId,
        [FromQuery] Guid? viewerId,
        [FromServices] ITabulationReportService tabulationReportService,
        CancellationToken cancellationToken)
    {
        var report = await tabulationReportService.GetTabulationAsync(tabulationId, participantId ?? Guid.Empty, viewerId ?? Guid.Empty, cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("tabulations/{tabulationId:guid}/published")]
    [ProducesResponseType<PublishedTabulationsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishedTabulationsResponse>> GetPublishedTabulations(
        Guid tabulationId, [FromServices] ITabulationReportService tabulationReportService, CancellationToken cancellationToken)
    {
        var page = await tabulationReportService.GetPublishedTabulationsAsync(tabulationId, cancellationToken);
        return page is null ? NotFound() : Ok(page);
    }

    [HttpGet("published-tabulations/{publishedTabulationId:guid}")]
    [ProducesResponseType<TabulationReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TabulationReportResponse>> GetPublishedTabulation(
        Guid publishedTabulationId,
        [FromQuery] Guid? participantId,
        [FromQuery] Guid? viewerId,
        [FromServices] ITabulationReportService tabulationReportService,
        CancellationToken cancellationToken)
    {
        var report = await tabulationReportService.GetPublishedTabulationAsync(publishedTabulationId, participantId ?? Guid.Empty, viewerId ?? Guid.Empty, cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("tabulations/{tabulationId:guid}/tests/{testId:guid}/assessment-results")]
    [ProducesResponseType<AssessmentResultsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AssessmentResultsResponse>> GetAssessmentResults(
        Guid tabulationId, Guid testId, [FromServices] ITabulationReportService tabulationReportService, CancellationToken cancellationToken) =>
        AssessmentResults(tabulationReportService, testId, tabulationId, false, cancellationToken);

    [HttpGet("published-tabulations/{publishedTabulationId:guid}/tests/{testId:guid}/assessment-results")]
    [ProducesResponseType<AssessmentResultsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AssessmentResultsResponse>> GetPublishedAssessmentResults(
        Guid publishedTabulationId, Guid testId, [FromServices] ITabulationReportService tabulationReportService, CancellationToken cancellationToken) =>
        AssessmentResults(tabulationReportService, testId, publishedTabulationId, true, cancellationToken);

    [HttpGet("tabulations/{tabulationId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetTabulationAttachment(
        Guid tabulationId, Guid attachmentId, [FromServices] ITabulationReportService tabulationReportService, CancellationToken cancellationToken) =>
        Attachment(tabulationReportService, tabulationId, false, attachmentId, cancellationToken);

    [HttpGet("published-tabulations/{publishedTabulationId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetPublishedTabulationAttachment(
        Guid publishedTabulationId, Guid attachmentId, [FromServices] ITabulationReportService tabulationReportService, CancellationToken cancellationToken) =>
        Attachment(tabulationReportService, publishedTabulationId, true, attachmentId, cancellationToken);

    private async Task<ActionResult<AssessmentResultsResponse>> AssessmentResults(
        ITabulationReportService tabulationReportService, Guid testId, Guid tabulationId, bool isPublished, CancellationToken cancellationToken)
    {
        var results = await tabulationReportService.GetAssessmentResultsAsync(testId, tabulationId, isPublished, cancellationToken);
        return results is null ? NotFound() : Ok(results);
    }

    // ExternalWeb Tabulations.aspx / TabulationView.aspx / AssessmentResults.aspx - every endpoint
    // below is scoped to the caller's own Participant/Viewer/Test Consultant access (re-checked
    // against spgTabulations - see ParticipantTabulationService), unlike the unscoped
    // tabulations/published-tabulations endpoints above, which InternalWeb staff use with no such
    // restriction.
    [HttpGet("tabulations/for-user")]
    [ProducesResponseType<PublishedTabulationSummaryListResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PublishedTabulationSummaryListResponse>> GetTabulationsForUser(
        [FromQuery] Guid? participantId,
        [FromQuery] Guid? viewerId,
        [FromQuery] Guid? testConsultantId,
        [FromServices] IParticipantTabulationService participantTabulationService,
        CancellationToken cancellationToken)
    {
        var tabulations = await participantTabulationService.GetTabulationsForUserAsync(participantId, viewerId, testConsultantId, cancellationToken);
        return Ok(tabulations);
    }

    [HttpGet("published-tabulations/{publishedTabulationId:guid}/for-user")]
    [ProducesResponseType<TabulationReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TabulationReportResponse>> GetPublishedTabulationForUser(
        Guid publishedTabulationId,
        [FromQuery] Guid? participantId,
        [FromQuery] Guid? viewerId,
        [FromQuery] Guid? testConsultantId,
        [FromServices] IParticipantTabulationService participantTabulationService,
        CancellationToken cancellationToken)
    {
        var report = await participantTabulationService.GetPublishedTabulationForUserAsync(publishedTabulationId, participantId, viewerId, testConsultantId, cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("published-tabulations/{publishedTabulationId:guid}/tests/{testId:guid}/assessment-results/for-user")]
    [ProducesResponseType<AssessmentResultsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessmentResultsResponse>> GetAssessmentResultsForUser(
        Guid publishedTabulationId,
        Guid testId,
        [FromQuery] Guid? participantId,
        [FromQuery] Guid? viewerId,
        [FromQuery] Guid? testConsultantId,
        [FromServices] IParticipantTabulationService participantTabulationService,
        CancellationToken cancellationToken)
    {
        var results = await participantTabulationService.GetAssessmentResultsForUserAsync(publishedTabulationId, testId, participantId, viewerId, testConsultantId, cancellationToken);
        return results is null ? NotFound() : Ok(results);
    }

    [HttpGet("published-tabulations/{publishedTabulationId:guid}/attachments/{attachmentId:guid}/for-user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublishedTabulationAttachmentForUser(
        Guid publishedTabulationId,
        Guid attachmentId,
        [FromQuery] Guid? participantId,
        [FromQuery] Guid? viewerId,
        [FromQuery] Guid? testConsultantId,
        [FromServices] IParticipantTabulationService participantTabulationService,
        CancellationToken cancellationToken)
    {
        var attachment = await participantTabulationService.GetAttachmentForUserAsync(publishedTabulationId, attachmentId, participantId, viewerId, testConsultantId, cancellationToken);
        return attachment is null ? NotFound() : AttachmentFile(attachment);
    }

    private async Task<IActionResult> Attachment(
        ITabulationReportService tabulationReportService, Guid tabulationId, bool isPublished, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await tabulationReportService.GetAttachmentAsync(tabulationId, isPublished, attachmentId, cancellationToken);
        return attachment is null ? NotFound() : AttachmentFile(attachment);
    }

    private FileContentResult AttachmentFile(TabulationSourceAttachment attachment)
    {
        var inline = InlineAttachmentContentTypes.Contains(attachment.ContentType);
        var contentType = inline ? attachment.ContentType : "application/octet-stream";
        var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(attachment.FileName);
        Response.Headers.ContentDisposition = disposition.ToString();
        return File(attachment.FileData, contentType);
    }
}
