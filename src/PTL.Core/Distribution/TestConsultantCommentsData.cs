namespace PTL.Core.Distribution;

// spgTestConsultantComments - legacy ResultsTabulation.TestConsultantComments.
public sealed class TestConsultantCommentsEntity
{
    public Guid MonthlyDistributionSchemeId { get; init; }
    public string SchemeName { get; init; } = string.Empty;
    public string SchemeIdentifier { get; init; } = string.Empty;
    public string DistributionReference { get; init; } = string.Empty;
    public string DistributionReferenceSuffix { get; init; } = string.Empty;
    public string ScheduleCode { get; init; } = string.Empty;
    public string TestConsultantComments { get; init; } = string.Empty;
    public bool IsCommentSubmitted { get; init; }

    // Legacy passed Guid.Empty back to spuMonthlyDistributionSchemeForTabulations when no author was set.
    public Guid TestConsultantCommentsAuthorId { get; init; }
    public string TestConsultantCommentsAuthor { get; init; } = string.Empty;
    public string TestConsultantCommentsDept { get; init; } = string.Empty;
    public bool RequiresAssessment { get; init; }
    public List<TabulationSourceAttachment> Attachments { get; } = [];

    public string DistributionReferenceFull => $"{DistributionReference}{DistributionReferenceSuffix}/{ScheduleCode}";

    public string SchemeNameFull => $"{SchemeIdentifier}: {SchemeName}";
}

// spiAuditMonthlyDistributionScheme parameters (legacy AuditableBusinessBase.Audit). The comments and
// file names are the new values; the Update flags say which of them changed.
public sealed record CommentsAudit(
    string AuditWho,
    string AuditWhy,
    string TestConsultantComments,
    bool TestConsultantCommentsUpdate,
    string FileNames,
    bool FileNamesUpdate);

// Legacy appSettings AllowedFileTypes / MaxFileSize (AppSettingsHelper defaults).
public sealed class AttachmentOptions
{
    public const string SectionName = "Attachments";

    private static readonly string[] DefaultAllowedFileTypes = [".pdf", ".jpeg", ".jpg", ".xls", ".xlsx", ".pptx"];

    // Configuration binding appends to a pre-filled array, so the defaults apply only when nothing is configured.
    public string[]? AllowedFileTypes { get; set; }

    public int MaxFileSize { get; set; } = 10485760;

    public IReadOnlyList<string> EffectiveAllowedFileTypes => AllowedFileTypes is { Length: > 0 } ? AllowedFileTypes : DefaultAllowedFileTypes;
}
