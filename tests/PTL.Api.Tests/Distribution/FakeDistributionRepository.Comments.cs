using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

internal sealed partial class FakeDistributionRepository
{
    public Dictionary<Guid, TestConsultantCommentsEntity> TestConsultantComments { get; } = [];

    public List<(Guid SchemeId, string Comments, Guid AuthorId, bool IsCommentSubmitted, CommentsAudit Audit, IReadOnlyList<Guid> Removed, IReadOnlyList<NewCommentsAttachment> Added)> UpdatedComments { get; } = [];

    public Task<TestConsultantCommentsEntity?> GetTestConsultantCommentsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(TestConsultantComments.GetValueOrDefault(monthlyDistributionSchemeId));

    public Task UpdateTestConsultantCommentsAsync(
        Guid monthlyDistributionSchemeId,
        string comments,
        Guid authorId,
        bool isCommentSubmitted,
        CommentsAudit audit,
        IReadOnlyList<Guid> removedAttachmentIds,
        IReadOnlyList<NewCommentsAttachment> newAttachments,
        CancellationToken cancellationToken = default)
    {
        UpdatedComments.Add((monthlyDistributionSchemeId, comments, authorId, isCommentSubmitted, audit, removedAttachmentIds, newAttachments));
        return Task.CompletedTask;
    }
}
