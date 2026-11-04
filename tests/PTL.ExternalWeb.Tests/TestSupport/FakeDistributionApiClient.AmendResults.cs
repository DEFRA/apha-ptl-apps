using PTL.Contracts.Distribution;

namespace PTL.ExternalWeb.Tests.TestSupport;

internal sealed partial class FakeDistributionApiClient
{
    public AmendResultsResponse? AmendResults { get; set; }
    public AmendResultsSaveResult? AmendResultsSaveResult { get; set; }
    public List<AmendResultsSaveRequest> SavedAmendResults { get; } = [];

    public Task<AmendResultsResponse?> GetAmendResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(AmendResults);

    public Task<AmendResultsSaveResult?> SaveAmendResultsAsync(Guid monthlyDistributionSchemeId, AmendResultsSaveRequest request, CancellationToken cancellationToken = default)
    {
        SavedAmendResults.Add(request);
        return Task.FromResult(AmendResultsSaveResult);
    }
}
