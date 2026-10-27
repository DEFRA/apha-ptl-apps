using System.Net;
using System.Net.Http.Json;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public enum ResultsSignOffApiStatus
{
    Done,
    NotFound,

    // Someone has already authorised or rejected the tabulation.
    NotAwaiting,

    // Authorising needs a signed-in user to sign it.
    NoSignatory,
}

public interface IResultsSignOffApiClient
{
    Task<ResultsSignOffQueueResponse> GetQueueAsync(CancellationToken cancellationToken = default);

    // Null when there is no such tabulation.
    Task<ResultsSignOffReviewResponse?> GetReviewAsync(Guid tabulationId, Guid participantId, Guid viewerId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default);

    Task<ResultsSignOffApiStatus> RejectAsync(Guid tabulationId, CancellationToken cancellationToken = default);

    Task<ResultsSignOffApiStatus> AuthoriseAsync(Guid tabulationId, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's /api/results-sign-off endpoints.
public sealed class ResultsSignOffApiClient(HttpClient httpClient) : IResultsSignOffApiClient
{
    public async Task<ResultsSignOffQueueResponse> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(new Uri("/api/results-sign-off/queue", UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResultsSignOffQueueResponse>(cancellationToken)
            ?? new ResultsSignOffQueueResponse([]);
    }

    public async Task<ResultsSignOffReviewResponse?> GetReviewAsync(Guid tabulationId, Guid participantId, Guid viewerId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = new List<string> { $"participantId={participantId}", $"viewerId={viewerId}" };
        query.AddRange(filter.HiddenSamples.Select(s => $"hiddenSamples={Uri.EscapeDataString(s)}"));
        if (filter.HideMethodItems)
        {
            query.Add("hideMethodItems=true");
        }

        using var response = await httpClient.GetAsync(new Uri($"/api/results-sign-off/tabulations/{tabulationId}?{string.Join('&', query)}", UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResultsSignOffReviewResponse>(cancellationToken);
    }

    public Task<ResultsSignOffApiStatus> RejectAsync(Guid tabulationId, CancellationToken cancellationToken = default) =>
        PostDecisionAsync(tabulationId, "reject", cancellationToken);

    // The signed-in user signs it: the acting user header the ActingUserDelegatingHandler adds to every request.
    public Task<ResultsSignOffApiStatus> AuthoriseAsync(Guid tabulationId, CancellationToken cancellationToken = default) =>
        PostDecisionAsync(tabulationId, "authorise", cancellationToken);

    private async Task<ResultsSignOffApiStatus> PostDecisionAsync(Guid tabulationId, string decision, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(new Uri($"/api/results-sign-off/tabulations/{tabulationId}/{decision}", UriKind.Relative), content: null, cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return ResultsSignOffApiStatus.NotFound;
            case HttpStatusCode.Conflict:
                return ResultsSignOffApiStatus.NotAwaiting;
            case HttpStatusCode.Forbidden:
                return ResultsSignOffApiStatus.NoSignatory;
            default:
                response.EnsureSuccessStatusCode();
                return ResultsSignOffApiStatus.Done;
        }
    }
}
