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
