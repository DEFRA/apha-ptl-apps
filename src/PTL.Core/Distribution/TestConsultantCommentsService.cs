using System.Globalization;
using Microsoft.Extensions.Options;
using PTL.Contracts.Distribution;
using PTL.Core.Scheme;

namespace PTL.Core.Distribution;

// View Comments - legacy Scheduling/ViewComments.aspx(.vb) with ResultsTabulation.TestConsultantComments.
// As in legacy, an upload or removal only changes the page until Save; the uploaded file waits in
// IAttachmentStagingStore (server memory, as legacy kept it in Session) and Save writes it to the database.
public interface ITestConsultantCommentsService
{
    Task<TestConsultantCommentsResponse?> GetAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<TestConsultantCommentsSaveResult?> SaveAsync(
        Guid monthlyDistributionSchemeId, TestConsultantCommentsSaveRequest request, string auditWho, CancellationToken cancellationToken = default);

    Task<CommentsAttachmentStageResult?> StageAttachmentAsync(
        Guid monthlyDistributionSchemeId, CommentsAttachmentStageRequest request, CancellationToken cancellationToken = default);

    // Legacy Cancel: the unsaved uploads are thrown away.
    Task DiscardStagedAttachmentsAsync(Guid monthlyDistributionSchemeId, IReadOnlyList<Guid> tokens, CancellationToken cancellationToken = default);

    Task<TabulationSourceAttachment?> GetAttachmentAsync(Guid monthlyDistributionSchemeId, Guid attachmentId, CancellationToken cancellationToken = default);

    Task<StagedAttachment?> GetStagedAttachmentAsync(Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default);
}

public sealed class TestConsultantCommentsService(
    IDistributionRepository distributionRepository,
    IAttachmentStagingStore stagingStore,
    IOptions<AttachmentOptions> attachmentOptions,
    TimeProvider timeProvider) : ITestConsultantCommentsService
{
    public const string CommentsKey = "Comments";
    public const string FileKey = "File";

    // Legacy ViewComments.aspx validator texts.
    public const string CommentsRequiredMessage = "Comments required.";
    public const string CommentsTooLongMessage = "Comments must not exceed 50,000 characters. Note that rich text such as font changes and bullet points will artificially count towards this character limit.";
    public const string OneAttachmentMessage = "Only one attachment is allowed.";
    public const string NoFileMessage = "Please select a file to upload.";

    private const int MaxCommentsLength = 50000;

    public async Task<TestConsultantCommentsResponse?> GetAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var comments = await distributionRepository.GetTestConsultantCommentsAsync(monthlyDistributionSchemeId, cancellationToken);
        if (comments is null)
        {
            return null;
        }

        var options = attachmentOptions.Value;
        return new TestConsultantCommentsResponse(
            comments.MonthlyDistributionSchemeId,
            $"{comments.DistributionReferenceFull} {comments.SchemeNameFull}",
            AuthorText(comments),
            SchemeInstructions.Sanitise(comments.TestConsultantComments),
            !(comments.IsCommentSubmitted || comments.RequiresAssessment),
            [.. comments.Attachments.Select(a => new CommentsAttachmentResponse(a.AttachmentId, a.FileName))],
            options.EffectiveAllowedFileTypes,
            string.Join(", ", options.EffectiveAllowedFileTypes.Select(t => $"{t.ToUpperInvariant().Replace(".", string.Empty, StringComparison.Ordinal)} ({t})")),
            MaxFileSizeMegabytes(options) + "MB");
    }

    public async Task<TestConsultantCommentsSaveResult?> SaveAsync(
        Guid monthlyDistributionSchemeId, TestConsultantCommentsSaveRequest request, string auditWho, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = await distributionRepository.GetTestConsultantCommentsAsync(monthlyDistributionSchemeId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var posted = request.Comments ?? string.Empty;
        if (!SchemeInstructions.HasContent(posted))
        {
            return Invalid(CommentsKey, CommentsRequiredMessage);
        }

        if (posted.Length > MaxCommentsLength)
        {
            return Invalid(CommentsKey, CommentsTooLongMessage);
        }

        var removed = (request.RemovedAttachmentIds ?? []).Where(id => current.Attachments.Exists(a => a.AttachmentId == id)).Distinct().ToList();
        var staged = new List<(Guid Token, StagedAttachment File)>();
        foreach (var token in (request.StagedAttachmentTokens ?? []).Distinct())
        {
            if (await stagingStore.GetAsync(monthlyDistributionSchemeId, token, cancellationToken) is { } file)
            {
                staged.Add((token, file));
            }
        }

        var remainingNames = current.Attachments.Where(a => !removed.Contains(a.AttachmentId)).Select(a => a.FileName).ToList();

        // Legacy TestConsultantComments ValidateSingleAttachment business rule.
        if (remainingNames.Count + staged.Count > 1)
        {
            return Invalid(FileKey, OneAttachmentMessage);
        }

        var comments = SchemeInstructions.Sanitise(posted);

        // Legacy: Submit sets the flag; Save also sets it for assessment schemes; once set it stays set.
        var isSubmitted = current.IsCommentSubmitted || current.RequiresAssessment || request.Submit;
        var commentsChanged = !string.Equals(comments, current.TestConsultantComments, StringComparison.Ordinal);
        var filesChanged = removed.Count > 0 || staged.Count > 0;

        if (!commentsChanged && !filesChanged && isSubmitted == current.IsCommentSubmitted)
        {
            return Saved();
        }

        if (string.IsNullOrWhiteSpace(request.AuditWhy))
        {
            return new TestConsultantCommentsSaveResult(false, true, AuditObjectName(current), new Dictionary<string, string>());
        }

        var uploadDate = timeProvider.GetLocalNow().DateTime;
        await distributionRepository.UpdateTestConsultantCommentsAsync(
            monthlyDistributionSchemeId,
            comments,
            current.TestConsultantCommentsAuthorId,
            isSubmitted,
            new CommentsAudit(auditWho, request.AuditWhy, comments, commentsChanged, string.Join(", ", remainingNames.Concat(staged.Select(s => s.File.FileName))), filesChanged),
            removed,
            [.. staged.Select(s => new NewCommentsAttachment(s.File.FileName, s.File.ContentType, s.File.FileData, uploadDate))],
            cancellationToken);

        foreach (var (token, _) in staged)
        {
            await stagingStore.DeleteAsync(monthlyDistributionSchemeId, token, cancellationToken);
        }

        return Saved();
    }

    public async Task<CommentsAttachmentStageResult?> StageAttachmentAsync(
        Guid monthlyDistributionSchemeId, CommentsAttachmentStageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = await distributionRepository.GetTestConsultantCommentsAsync(monthlyDistributionSchemeId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var options = attachmentOptions.Value;
        var removed = request.RemovedAttachmentIds ?? [];
        var attachmentCount = current.Attachments.Count(a => !removed.Contains(a.AttachmentId)) + (request.StagedAttachmentTokens ?? []).Distinct().Count();

        // Legacy FileUploadValidator_ServerValidate, in its order.
        if (attachmentCount > 0)
        {
            return StageInvalid(OneAttachmentMessage);
        }

        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileData is not { Length: > 0 })
        {
            return StageInvalid(NoFileMessage);
        }

        var fileName = Path.GetFileName(request.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!options.EffectiveAllowedFileTypes.Contains(extension, StringComparer.Ordinal))
        {
            return StageInvalid("Only " + string.Join(", ", options.EffectiveAllowedFileTypes.Select(t => t.ToUpperInvariant().Replace(".", string.Empty, StringComparison.Ordinal))) + " files are allowed.");
        }

        if (request.FileData.Length > options.MaxFileSize)
        {
            return StageInvalid($"File size exceeds the {MaxFileSizeMegabytes(options)}MB limit.");
        }

        var token = Guid.NewGuid();
        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType;
        await stagingStore.SaveAsync(monthlyDistributionSchemeId, token, new StagedAttachment(fileName, contentType, request.FileData), cancellationToken);

        return new CommentsAttachmentStageResult(true, token, fileName, new Dictionary<string, string>());
    }

    public async Task DiscardStagedAttachmentsAsync(Guid monthlyDistributionSchemeId, IReadOnlyList<Guid> tokens, CancellationToken cancellationToken = default)
    {
        foreach (var token in (tokens ?? []).Distinct())
        {
            await stagingStore.DeleteAsync(monthlyDistributionSchemeId, token, cancellationToken);
        }
    }

    public async Task<TabulationSourceAttachment?> GetAttachmentAsync(Guid monthlyDistributionSchemeId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var current = await distributionRepository.GetTestConsultantCommentsAsync(monthlyDistributionSchemeId, cancellationToken);
        return current?.Attachments.Find(a => a.AttachmentId == attachmentId);
    }

    public Task<StagedAttachment?> GetStagedAttachmentAsync(Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default) =>
        stagingStore.GetAsync(monthlyDistributionSchemeId, token, cancellationToken);

    private static string AuthorText(TestConsultantCommentsEntity comments)
    {
        if (comments.TestConsultantCommentsAuthor.Length == 0)
        {
            return "No Comments have been provided. Add your own comments below.";
        }

        return comments.TestConsultantCommentsDept.Length == 0
            ? "Comments provided by " + comments.TestConsultantCommentsAuthor
            : $"Comments provided by {comments.TestConsultantCommentsAuthor}, {comments.TestConsultantCommentsDept}";
    }

    // Legacy TestConsultantComments.AuditObjectName.
    private static string AuditObjectName(TestConsultantCommentsEntity comments) => "Comments for Distribution " + comments.DistributionReferenceFull;

    // Legacy: maxFileSize / 1048576 (a VB floating-point division).
    private static string MaxFileSizeMegabytes(AttachmentOptions options) =>
        (options.MaxFileSize / 1048576d).ToString(CultureInfo.GetCultureInfo("en-GB"));

    private static TestConsultantCommentsSaveResult Saved() => new(true, false, string.Empty, new Dictionary<string, string>());

    private static TestConsultantCommentsSaveResult Invalid(string key, string message) =>
        new(false, false, string.Empty, new Dictionary<string, string> { [key] = message });

    private static CommentsAttachmentStageResult StageInvalid(string message) =>
        new(false, Guid.Empty, string.Empty, new Dictionary<string, string> { [FileKey] = message });
}
