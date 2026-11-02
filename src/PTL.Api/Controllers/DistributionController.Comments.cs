using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// View Comments (legacy Scheduling/ViewComments.aspx): the Test Consultant comments for a scheme,
// Save/Submit (audited through spiAuditMonthlyDistributionScheme) and its single attachment.
public sealed partial class DistributionController
{
    [HttpGet("schemes/{monthlyDistributionSchemeId:guid}/comments")]
    [ProducesResponseType<TestConsultantCommentsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestConsultantCommentsResponse>> GetTestConsultantComments(
        Guid monthlyDistributionSchemeId, [FromServices] ITestConsultantCommentsService commentsService, CancellationToken cancellationToken)
    {
        var comments = await commentsService.GetAsync(monthlyDistributionSchemeId, cancellationToken);
        return comments is null ? NotFound() : Ok(comments);
    }

    [HttpPut("schemes/{monthlyDistributionSchemeId:guid}/comments")]
    [ProducesResponseType<TestConsultantCommentsSaveResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestConsultantCommentsSaveResult>> SaveTestConsultantComments(
        Guid monthlyDistributionSchemeId,
        TestConsultantCommentsSaveRequest request,
        [FromServices] ITestConsultantCommentsService commentsService,
        CancellationToken cancellationToken)
    {
        var result = await commentsService.SaveAsync(monthlyDistributionSchemeId, request, await AuditWhoAsync(cancellationToken), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // Legacy maxRequestLength was 15MB; the configured file limit (10MB) is checked by the service.
    [HttpPost("schemes/{monthlyDistributionSchemeId:guid}/comments/attachments/staged")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType<CommentsAttachmentStageResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentsAttachmentStageResult>> StageTestConsultantCommentsAttachment(
        Guid monthlyDistributionSchemeId,
        CommentsAttachmentStageRequest request,
        [FromServices] ITestConsultantCommentsService commentsService,
        CancellationToken cancellationToken)
    {
        var result = await commentsService.StageAttachmentAsync(monthlyDistributionSchemeId, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("schemes/{monthlyDistributionSchemeId:guid}/comments/attachments/staged/discard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DiscardStagedTestConsultantCommentsAttachments(
        Guid monthlyDistributionSchemeId,
        CommentsStagedAttachmentsRequest request,
        [FromServices] ITestConsultantCommentsService commentsService,
        CancellationToken cancellationToken)
    {
        await commentsService.DiscardStagedAttachmentsAsync(monthlyDistributionSchemeId, request?.Tokens ?? [], cancellationToken);
        return NoContent();
    }

    [HttpGet("schemes/{monthlyDistributionSchemeId:guid}/comments/attachments/staged/{token:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStagedTestConsultantCommentsAttachment(
        Guid monthlyDistributionSchemeId, Guid token, [FromServices] ITestConsultantCommentsService commentsService, CancellationToken cancellationToken)
    {
        var staged = await commentsService.GetStagedAttachmentAsync(monthlyDistributionSchemeId, token, cancellationToken);
        return staged is null
            ? NotFound()
            : AttachmentFile(new TabulationSourceAttachment(token, staged.FileName, staged.ContentType, staged.FileData));
    }

    [HttpGet("schemes/{monthlyDistributionSchemeId:guid}/comments/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTestConsultantCommentsAttachment(
        Guid monthlyDistributionSchemeId, Guid attachmentId, [FromServices] ITestConsultantCommentsService commentsService, CancellationToken cancellationToken)
    {
        var attachment = await commentsService.GetAttachmentAsync(monthlyDistributionSchemeId, attachmentId, cancellationToken);
        return attachment is null ? NotFound() : AttachmentFile(attachment);
    }

    // The signed-in internal user's login name, forwarded by InternalWeb; "Guest" when none was.
    private Task<string> AuditWhoAsync(CancellationToken cancellationToken) => auditActor.GetAuditNameAsync(cancellationToken);
}
