using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeResultsSignOffApiClient : IResultsSignOffApiClient
{
    public ResultsSignOffQueueResponse Queue { get; set; } = new([]);

    public ResultsSignOffApiStatus RejectStatus { get; set; } = ResultsSignOffApiStatus.Done;

    public ResultsSignOffApiStatus AuthoriseStatus { get; set; } = ResultsSignOffApiStatus.Done;

    public ResultsSignOffReviewResponse? Review { get; set; }

    public List<(Guid TabulationId, Guid ParticipantId, Guid ViewerId, TestConsultantTabulationFilter Filter)> RequestedReviews { get; } = [];

    public Task<ResultsSignOffReviewResponse?> GetReviewAsync(Guid tabulationId, Guid participantId, Guid viewerId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
    {
        RequestedReviews.Add((tabulationId, participantId, viewerId, filter));
        return Task.FromResult(Review);
    }

    public List<Guid> Rejected { get; } = [];

    public List<Guid> Authorised { get; } = [];

    public Task<ResultsSignOffApiStatus> AuthoriseAsync(Guid tabulationId, CancellationToken cancellationToken = default)
    {
        Authorised.Add(tabulationId);
        return Task.FromResult(AuthoriseStatus);
    }

    public Task<ResultsSignOffQueueResponse> GetQueueAsync(CancellationToken cancellationToken = default) => Task.FromResult(Queue);

    public Task<ResultsSignOffApiStatus> RejectAsync(Guid tabulationId, CancellationToken cancellationToken = default)
    {
        Rejected.Add(tabulationId);
        return Task.FromResult(RejectStatus);
    }
}
