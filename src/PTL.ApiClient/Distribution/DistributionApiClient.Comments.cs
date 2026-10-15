using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public partial interface IDistributionApiClient
{
    Task<TestConsultantCommentsResponse?> GetTestConsultantCommentsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<TestConsultantCommentsSaveResult?> SaveTestConsultantCommentsAsync(
        Guid monthlyDistributionSchemeId, TestConsultantCommentsSaveRequest request, CancellationToken cancellationToken = default);

    Task<CommentsAttachmentStageResult?> StageTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, CommentsAttachmentStageRequest request, CancellationToken cancellationToken = default);

    Task DiscardStagedTestConsultantCommentsAttachmentsAsync(
        Guid monthlyDistributionSchemeId, IReadOnlyList<Guid> tokens, CancellationToken cancellationToken = default);

    Task<TabulationAttachmentFile?> GetTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, Guid attachmentId, CancellationToken cancellationToken = default);

    Task<TabulationAttachmentFile?> GetStagedTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default);
}

public sealed partial class DistributionApiClient
{
    public Task<TestConsultantCommentsResponse?> GetTestConsultantCommentsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<TestConsultantCommentsResponse>($"/api/distributions/schemes/{monthlyDistributionSchemeId}/comments", cancellationToken);

    public async Task<TestConsultantCommentsSaveResult?> SaveTestConsultantCommentsAsync(
        Guid monthlyDistributionSchemeId, TestConsultantCommentsSaveRequest request, CancellationToken cancellationToken = default) =>
        await ReadOrNullAsync<TestConsultantCommentsSaveResult>(
            await httpClient.PutAsJsonAsync($"/api/distributions/schemes/{monthlyDistributionSchemeId}/comments", request, cancellationToken),
            cancellationToken);

    public async Task<CommentsAttachmentStageResult?> StageTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, CommentsAttachmentStageRequest request, CancellationToken cancellationToken = default) =>
        await ReadOrNullAsync<CommentsAttachmentStageResult>(
            await httpClient.PostAsJsonAsync($"/api/distributions/schemes/{monthlyDistributionSchemeId}/comments/attachments/staged", request, cancellationToken),
            cancellationToken);

    public async Task DiscardStagedTestConsultantCommentsAttachmentsAsync(
        Guid monthlyDistributionSchemeId, IReadOnlyList<Guid> tokens, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/distributions/schemes/{monthlyDistributionSchemeId}/comments/attachments/staged/discard", new CommentsStagedAttachmentsRequest(tokens), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<TabulationAttachmentFile?> GetTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, Guid attachmentId, CancellationToken cancellationToken = default) =>
        GetAttachmentFileAsync($"/api/distributions/schemes/{monthlyDistributionSchemeId}/comments/attachments/{attachmentId}", cancellationToken);

    public Task<TabulationAttachmentFile?> GetStagedTestConsultantCommentsAttachmentAsync(
        Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default) =>
        GetAttachmentFileAsync($"/api/distributions/schemes/{monthlyDistributionSchemeId}/comments/attachments/staged/{token}", cancellationToken);

    private static async Task<T?> ReadOrNullAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }
}
