using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.Distribution;

// View Comments - legacy Scheduling/ViewComments.aspx, reached from the Tabulations page's
// "View Comments" action. Save/Submit/Cancel return to the Tabulations page as legacy did; uploads
// and removals only take effect on Save, and Cancel throws them away.
public sealed partial class DistributionController
{
    private const string SubmitCommand = "submit";
    private const string AuditWhyField = nameof(ViewCommentsViewModel.AuditWhy);
    private const string CommentsField = nameof(ViewCommentsViewModel.Comments);
    private const string FileField = "File";

    [HttpGet]
    public async Task<IActionResult> ViewComments(int year, int monthId, Guid schemeId, CancellationToken cancellationToken)
    {
        var comments = await distributionApiClient.GetTestConsultantCommentsAsync(schemeId, cancellationToken);
        if (comments is null)
        {
            return NotFound();
        }

        var model = ToViewCommentsViewModel(year, monthId, comments);
        model.Comments = comments.CommentsHtml;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewComments(
        int year, int monthId, Guid schemeId, ViewCommentsViewModel model, string? command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            await DiscardStagedAsync(schemeId, model, cancellationToken);
            return RedirectToAction(nameof(Tabulations), new { year, monthId });
        }

        // Legacy Audit.aspx Cancel: back to the page with the unsaved changes still in place.
        if (string.Equals(command, "cancelAudit", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.Clear();
            model.IsAuditStep = false;
            model.AuditWhy = null;
            model.PendingCommand = null;
            return await RedisplayViewCommentsAsync(year, monthId, schemeId, model, cancellationToken);
        }

        if (model.IsAuditStep is true && string.IsNullOrWhiteSpace(model.AuditWhy))
        {
            ModelState.AddModelError(AuditWhyField, "The reason for these changes must be specified");
        }

        if (!ModelState.IsValid)
        {
            return await RedisplayViewCommentsAsync(year, monthId, schemeId, model, cancellationToken);
        }

        var submit = string.Equals(command ?? model.PendingCommand, SubmitCommand, StringComparison.OrdinalIgnoreCase);
        var result = await distributionApiClient.SaveTestConsultantCommentsAsync(
            schemeId,
            new TestConsultantCommentsSaveRequest(
                model.Comments, submit, model.IsAuditStep is true ? model.AuditWhy : null, [.. model.StagedFiles.Select(f => f.Token)], model.RemovedAttachmentIds),
            cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        if (result.AuditReasonRequired)
        {
            model.IsAuditStep = true;
            model.AuditObjectName = result.AuditObjectName;
            model.PendingCommand = submit ? SubmitCommand : "save";
            return await RedisplayViewCommentsAsync(year, monthId, schemeId, model, cancellationToken);
        }

        if (!result.Success)
        {
            foreach (var (key, message) in result.Errors)
            {
                ModelState.AddModelError(key == "Comments" ? CommentsField : FileField, message);
            }

            return await RedisplayViewCommentsAsync(year, monthId, schemeId, model, cancellationToken);
        }

        return RedirectToAction(nameof(Tabulations), new { year, monthId });
    }

    // Legacy BtnUploadFile_Click: validated now, held until Save. Size limit is the legacy 15MB
    // maxRequestLength; the configured 10MB file limit is enforced by the API.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(15 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 15 * 1024 * 1024)]
    public async Task<IActionResult> ViewCommentsUpload(
        int year, int monthId, Guid schemeId, ViewCommentsViewModel model, IFormFile? file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Comments is only echoed back on an upload; its Required rule applies at Save.
        ModelState.Remove(CommentsField);
        if (!ModelState.IsValid)
        {
            return await RedisplayViewCommentsAsync(year, monthId, schemeId, model, cancellationToken);
        }

        byte[]? data = null;
        if (file is { Length: > 0 })
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            data = stream.ToArray();
        }

        var result = await distributionApiClient.StageTestConsultantCommentsAttachmentAsync(
            schemeId,
            new CommentsAttachmentStageRequest(file?.FileName, file?.ContentType, data, [.. model.StagedFiles.Select(f => f.Token)], model.RemovedAttachmentIds),
            cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        ModelState.Clear();
        if (result.Success)
        {
            model.StagedAttachments.Add($"{result.Token}|{result.FileName}");
        }
        else
        {
            foreach (var message in result.Errors.Values)
            {
                ModelState.AddModelError(FileField, message);
            }
        }

        return await RedisplayViewCommentsAsync(year, monthId, schemeId, model, cancellationToken);
    }

    // Legacy AttachmentsList delete: a saved file is marked for removal, an unsaved upload is dropped.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewCommentsRemoveAttachment(
        int year, int monthId, Guid schemeId, Guid attachmentId, ViewCommentsViewModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Comments is only echoed back when a file is removed; its Required rule applies at Save.
        ModelState.Remove(CommentsField);
        if (!ModelState.IsValid)
        {
            return await RedisplayViewCommentsAsync(year, monthId, schemeId, model, cancellationToken);
        }

        ModelState.Clear();
        var staged = model.StagedFiles.FirstOrDefault(f => f.Token == attachmentId);
        if (staged.Token != Guid.Empty)
        {
            model.StagedAttachments.RemoveAll(s => s.StartsWith(attachmentId + "|", StringComparison.OrdinalIgnoreCase));
            await distributionApiClient.DiscardStagedTestConsultantCommentsAttachmentsAsync(schemeId, [attachmentId], cancellationToken);
        }
        else if (!model.RemovedAttachmentIds.Contains(attachmentId))
        {
            model.RemovedAttachmentIds.Add(attachmentId);
        }

        return await RedisplayViewCommentsAsync(year, monthId, schemeId, model, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> ViewCommentsAttachment(Guid schemeId, Guid attachmentId, CancellationToken cancellationToken) =>
        AttachmentResult(await distributionApiClient.GetTestConsultantCommentsAttachmentAsync(schemeId, attachmentId, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> ViewCommentsStagedAttachment(Guid schemeId, Guid token, CancellationToken cancellationToken) =>
        AttachmentResult(await distributionApiClient.GetStagedTestConsultantCommentsAttachmentAsync(schemeId, token, cancellationToken));

    private IActionResult AttachmentResult(PTL.ApiClient.Distribution.TabulationAttachmentFile? file)
    {
        if (file is null)
        {
            return NotFound();
        }

        Response.Headers.ContentDisposition = file.ContentDisposition;
        return File(file.Content, file.ContentType);
    }

    private async Task DiscardStagedAsync(Guid schemeId, ViewCommentsViewModel model, CancellationToken cancellationToken)
    {
        var tokens = model.StagedFiles.Select(f => f.Token).ToList();
        if (tokens.Count > 0)
        {
            await distributionApiClient.DiscardStagedTestConsultantCommentsAttachmentsAsync(schemeId, tokens, cancellationToken);
        }
    }

    // There is no server-side session (legacy kept the object in Session), so re-read the page
    // and keep the posted, unsaved values.
    private async Task<IActionResult> RedisplayViewCommentsAsync(
        int year, int monthId, Guid schemeId, ViewCommentsViewModel posted, CancellationToken cancellationToken)
    {
        var comments = await distributionApiClient.GetTestConsultantCommentsAsync(schemeId, cancellationToken);
        if (comments is null)
        {
            return NotFound();
        }

        var model = ToViewCommentsViewModel(year, monthId, comments);
        model.Comments = posted.Comments;
        model.StagedAttachments = posted.StagedAttachments;
        model.RemovedAttachmentIds = posted.RemovedAttachmentIds;
        model.IsAuditStep = posted.IsAuditStep;
        model.AuditObjectName = posted.AuditObjectName;
        model.PendingCommand = posted.PendingCommand;
        model.AuditWhy = posted.AuditWhy;
        return View(nameof(ViewComments), model);
    }

    private static ViewCommentsViewModel ToViewCommentsViewModel(int year, int monthId, TestConsultantCommentsResponse comments) => new()
    {
        YearId = year,
        MonthId = monthId,
        MonthlyDistributionSchemeId = comments.MonthlyDistributionSchemeId,
        DistributionTitle = comments.DistributionTitle,
        AuthorText = comments.AuthorText,
        CanSubmit = comments.CanSubmit,
        SavedAttachments = comments.Attachments,
        AllowedFileTypes = comments.AllowedFileTypes,
        AllowedFileTypesText = comments.AllowedFileTypesText,
        MaxFileSizeText = comments.MaxFileSizeText,
    };
}
