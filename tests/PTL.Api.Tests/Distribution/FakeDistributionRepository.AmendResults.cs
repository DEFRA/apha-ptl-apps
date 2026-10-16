using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

internal sealed partial class FakeDistributionRepository
{
    public Dictionary<Guid, AmendResultsSource> AmendResults { get; } = [];

    public List<AmendRatingOption> RatingOptions { get; } = [];

    public List<AmendResultsWrite> SavedAmendResults { get; } = [];

    public Task<AmendResultsSource?> GetAmendResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(AmendResults.GetValueOrDefault(monthlyDistributionSchemeId));

    public Task<IReadOnlyList<AmendRatingOption>> GetRatingOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AmendRatingOption>>(RatingOptions);

    public Task SaveAmendResultsAsync(AmendResultsWrite write, CancellationToken cancellationToken = default)
    {
        SavedAmendResults.Add(write);
        return Task.CompletedTask;
    }
}
