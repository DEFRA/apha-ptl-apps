using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public sealed partial class DistributionApiClient
{
    public Task<TabulationReportResponse?> GetTabulationAsync(Guid tabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<TabulationReportResponse>(
            $"/api/distributions/tabulations/{tabulationId}?participantId={participantId}&viewerId={viewerId}", cancellationToken);

    public Task<PublishedTabulationsResponse?> GetPublishedTabulationsAsync(Guid tabulationId, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<PublishedTabulationsResponse>($"/api/distributions/tabulations/{tabulationId}/published", cancellationToken);

    public Task<TabulationReportResponse?> GetPublishedTabulationAsync(Guid publishedTabulationId, Guid participantId, Guid viewerId, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<TabulationReportResponse>(
            $"/api/distributions/published-tabulations/{publishedTabulationId}?participantId={participantId}&viewerId={viewerId}", cancellationToken);

    public Task<AssessmentResultsResponse?> GetAssessmentResultsAsync(Guid tabulationId, Guid testId, bool isPublished, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<AssessmentResultsResponse>(
            $"/api/distributions/{TabulationSegment(isPublished)}/{tabulationId}/tests/{testId}/assessment-results", cancellationToken);

    public async Task<TabulationAttachmentFile?> GetTabulationAttachmentAsync(Guid tabulationId, bool isPublished, Guid attachmentId, CancellationToken cancellationToken = default) =>
        await GetAttachmentFileAsync($"/api/distributions/{TabulationSegment(isPublished)}/{tabulationId}/attachments/{attachmentId}", cancellationToken);

    private async Task<TabulationAttachmentFile?> GetAttachmentFileAsync(string url, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(new Uri(url, UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return new TabulationAttachmentFile(
            await response.Content.ReadAsByteArrayAsync(cancellationToken),
            response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
            response.Content.Headers.ContentDisposition?.ToString() ?? "attachment");
    }

    private static string TabulationSegment(bool isPublished) => isPublished ? "published-tabulations" : "tabulations";

    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        var response = await httpClient.GetAsync(new Uri(url, UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }
}
