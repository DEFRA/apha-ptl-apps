namespace PTL.Contracts.Distribution;

// View Comments (legacy Scheduling/ViewComments.aspx, sitemap "View Comments").
public sealed record TestConsultantCommentsResponse(
    Guid MonthlyDistributionSchemeId,
    string DistributionTitle,
    string AuthorText,
    string CommentsHtml,
    bool CanSubmit,
    IReadOnlyList<CommentsAttachmentResponse> Attachments,
    IReadOnlyList<string> AllowedFileTypes,
    string AllowedFileTypesText,
    string MaxFileSizeText);

public sealed record CommentsAttachmentResponse(Guid AttachmentId, string FileName);

// Submit = the legacy Submit button (stops the Test Consultants editing); otherwise Save. The
// attachment changes made on the page since it was opened are only written by this save.
public sealed record TestConsultantCommentsSaveRequest(
    string? Comments,
    bool Submit,
    string? AuditWhy = null,
    IReadOnlyList<Guid>? StagedAttachmentTokens = null,
    IReadOnlyList<Guid>? RemovedAttachmentIds = null);

// Legacy BtnUploadFile_Click: validated and held (in S3 staging) until Save.
public sealed record CommentsAttachmentStageRequest(
    string? FileName,
    string? ContentType,
    byte[]? FileData,
    IReadOnlyList<Guid>? StagedAttachmentTokens,
    IReadOnlyList<Guid>? RemovedAttachmentIds);

public sealed record CommentsAttachmentStageResult(bool Success, Guid Token, string FileName, IReadOnlyDictionary<string, string> Errors);

public sealed record CommentsStagedAttachmentsRequest(IReadOnlyList<Guid> Tokens);

// Errors are keyed by field: "Comments", "File" or "AuditWhy". AuditReasonRequired = legacy
// diverted the save through Audit.aspx; resubmit with AuditWhy.
public sealed record TestConsultantCommentsSaveResult(
    bool Success,
    bool AuditReasonRequired,
    string AuditObjectName,
    IReadOnlyDictionary<string, string> Errors);
