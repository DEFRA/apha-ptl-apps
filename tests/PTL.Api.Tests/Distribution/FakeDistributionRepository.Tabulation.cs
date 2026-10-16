using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

internal sealed partial class FakeDistributionRepository
{
    public Dictionary<Guid, TabulationSource> TabulationSources { get; } = [];

    public Dictionary<Guid, TabulationSource> PublishedTabulationSources { get; } = [];

    public Dictionary<Guid, PublishedTabulationsSource> PublishedTabulations { get; } = [];

    public Dictionary<(Guid TestId, Guid TabulationId, bool IsPublished), AssessmentScoreSource> AssessmentScores { get; } = [];

    public Task<TabulationSource?> GetTabulationSourceAsync(Guid tabulationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(TabulationSources.GetValueOrDefault(tabulationId));

    public Task<TabulationSource?> GetPublishedTabulationSourceAsync(Guid publishedTabulationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PublishedTabulationSources.GetValueOrDefault(publishedTabulationId));

    public Task<PublishedTabulationsSource?> GetPublishedTabulationsAsync(Guid tabulationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PublishedTabulations.GetValueOrDefault(tabulationId));

    public Task<AssessmentScoreSource?> GetAssessmentScoresAsync(Guid testId, Guid tabulationId, bool isPublished, CancellationToken cancellationToken = default) =>
        Task.FromResult(AssessmentScores.GetValueOrDefault((testId, tabulationId, isPublished)));
}
