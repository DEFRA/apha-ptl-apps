using PTL.Contracts.Distribution;

namespace PTL.ExternalWeb.Tests.TestSupport;

internal sealed partial class FakeDistributionApiClient
{
    public TabulationsResponse? TabulationsPreview { get; set; }
    public TabulationSendResponse? TabulationStage { get; set; }
    public bool SaveTabulationsResult { get; set; } = true;
    public List<IReadOnlyList<TabulationStagedSendRequest>> PreviewedTabulations { get; } = [];
    public List<TabulationStageRequest> StagedTabulationSends { get; } = [];
    public List<IReadOnlyList<TabulationStagedSendRequest>> SavedTabulations { get; } = [];

    public Task<TabulationsResponse?> PreviewTabulationsAsync(int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default)
    {
        PreviewedTabulations.Add(staged);
        return Task.FromResult(TabulationsPreview);
    }

    public Task<TabulationSendResponse?> StageTabulationSendAsync(int yearId, int monthId, TabulationStageRequest request, CancellationToken cancellationToken = default)
    {
        StagedTabulationSends.Add(request);
        return Task.FromResult(TabulationStage);
    }

    public Task<bool> SaveTabulationsAsync(int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default)
    {
        SavedTabulations.Add(staged);
        return Task.FromResult(SaveTabulationsResult);
    }
}
