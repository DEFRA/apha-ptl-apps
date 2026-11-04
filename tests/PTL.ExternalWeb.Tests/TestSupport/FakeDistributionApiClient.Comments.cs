using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.ExternalWeb.Tests.TestSupport;

internal sealed partial class FakeDistributionApiClient
{
    public TestConsultantCommentsResponse? TestConsultantComments { get; set; }
    public TestConsultantCommentsSaveResult? CommentsSaveResult { get; set; }
    public CommentsAttachmentStageResult? CommentsStageResult { get; set; }
    public TabulationAttachmentFile? CommentsAttachment { get; set; }
    public TabulationAttachmentFile? StagedCommentsAttachment { get; set; }
    public List<TestConsultantCommentsSaveRequest> SavedComments { get; } = [];
    public List<CommentsAttachmentStageRequest> StagedAttachmentRequests { get; } = [];
    public List<IReadOnlyList<Guid>> DiscardedStagedAttachments { get; } = [];

    public Task<TestConsultantCommentsResponse?> GetTestConsultantCommentsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(TestConsultantComments);

    public Task<TestConsultantCommentsSaveResult?> SaveTestConsultantCommentsAsync(
        Guid monthlyDistributionSchemeId, TestConsultantCommentsSaveRequest request, CancellationToken cancellationToken = default)
    {
        SavedComments.Add(request);
        return Task.FromResult(CommentsSaveResult);
    }

    public Task<CommentsAttachmentStageResult?> StageTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, CommentsAttachmentStageRequest request, CancellationToken cancellationToken = default)
    {
        StagedAttachmentRequests.Add(request);
        return Task.FromResult(CommentsStageResult);
    }

    public Task DiscardStagedTestConsultantCommentsAttachmentsAsync(
        Guid monthlyDistributionSchemeId, IReadOnlyList<Guid> tokens, CancellationToken cancellationToken = default)
    {
        DiscardedStagedAttachments.Add(tokens);
        return Task.CompletedTask;
    }

    public Task<TabulationAttachmentFile?> GetTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, Guid attachmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CommentsAttachment);

    public Task<TabulationAttachmentFile?> GetStagedTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default) =>
        Task.FromResult(StagedCommentsAttachment);
}
