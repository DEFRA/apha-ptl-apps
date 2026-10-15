namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // spgTestConsultantComments + spgMonthlyDistributionSchemeAttachments. Null when the scheme does not exist.
    Task<TestConsultantCommentsEntity?> GetTestConsultantCommentsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    // One transaction, as legacy TestConsultantComments.DataPortal_Update: spiAuditMonthlyDistributionScheme, spuMonthlyDistributionSchemeForTabulations, then the attachment list update
    // (spuMonthlyDistributionSchemeAttachmentSetDeleted for removals, spiMonthlyDistributionSchemeAttachment for new files).
    Task UpdateTestConsultantCommentsAsync(
        Guid monthlyDistributionSchemeId,
        string comments,
        Guid authorId,
        bool isCommentSubmitted,
        CommentsAudit audit,
        IReadOnlyList<Guid> removedAttachmentIds,
        IReadOnlyList<NewCommentsAttachment> newAttachments,
        CancellationToken cancellationToken = default);
}

public sealed record NewCommentsAttachment(string FileName, string ContentType, byte[] FileData, DateTime UploadDate);
