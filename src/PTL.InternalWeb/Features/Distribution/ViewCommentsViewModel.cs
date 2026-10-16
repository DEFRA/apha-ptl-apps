using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.Distribution;

// View Comments (legacy Scheduling/ViewComments.aspx). One multipart form holds the comments editor
// and the attachment controls. Attachment changes are kept in the form (StagedAttachments /
// RemovedAttachmentIds) until Save, as legacy kept them in Session.
public sealed class ViewCommentsViewModel
{
    // Display-only: set from the route and the API, never from the posted form.
    [BindNever]
    public int YearId { get; set; }
    [BindNever]
    public int MonthId { get; set; }
    [BindNever]
    public Guid MonthlyDistributionSchemeId { get; set; }

    public string DistributionTitle { get; set; } = string.Empty;
    public string AuthorText { get; set; } = string.Empty;
    [BindNever]
    public bool CanSubmit { get; set; }
    public IReadOnlyList<CommentsAttachmentResponse> SavedAttachments { get; set; } = [];
    public IReadOnlyList<string> AllowedFileTypes { get; set; } = [];
    public string AllowedFileTypesText { get; set; } = string.Empty;
    public string MaxFileSizeText { get; set; } = string.Empty;

    // Legacy ValidatorCommentsRequired / ValidatorCommentsRegEx.
    [Required(ErrorMessage = "Comments required.")]
    [StringLength(50000, ErrorMessage = "Comments must not exceed 50,000 characters. Note that rich text such as font changes and bullet points will artificially count towards this character limit.")]
    public string? Comments { get; set; }

    // Uploaded but not saved: "{token}|{fileName}".
    public List<string> StagedAttachments { get; set; } = [];

    // Saved attachments removed on the page but not saved yet.
    public List<Guid> RemovedAttachmentIds { get; set; } = [];

    // Legacy Audit.aspx, shown once Save/Submit has changed something.
    public bool IsAuditStep { get; set; }
    public string? AuditObjectName { get; set; }
    public string? PendingCommand { get; set; }

    [StringLength(2000)]
    public string? AuditWhy { get; set; }

    public IReadOnlyList<(Guid Token, string FileName)> StagedFiles =>
        [.. StagedAttachments
            .Select(s => s.Split('|', 2))
            .Where(p => p.Length == 2 && Guid.TryParse(p[0], out _))
            .Select(p => (Guid.Parse(p[0]), p[1]))];

    // What the page shows: the saved attachments not removed, then the unsaved uploads.
    public IReadOnlyList<ViewCommentsAttachment> Attachments =>
        [.. SavedAttachments
            .Where(a => !RemovedAttachmentIds.Contains(a.AttachmentId))
            .Select(a => new ViewCommentsAttachment(a.AttachmentId, a.FileName, false)),
         .. StagedFiles.Select(s => new ViewCommentsAttachment(s.Token, s.FileName, true))];
}

// IsStaged: uploaded on this page and not saved yet (Id is then the staging token).
public sealed record ViewCommentsAttachment(Guid Id, string FileName, bool IsStaged);
