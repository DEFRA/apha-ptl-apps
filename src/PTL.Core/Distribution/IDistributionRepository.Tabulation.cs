namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // spgDistributionTabulation. Null when the tabulation does not exist.
    Task<TabulationSource?> GetTabulationSourceAsync(Guid tabulationId, CancellationToken cancellationToken = default);

    // spgPublishedTabulation. Null when the issued tabulation does not exist or is older than the retention window.
    Task<TabulationSource?> GetPublishedTabulationSourceAsync(Guid publishedTabulationId, CancellationToken cancellationToken = default);

    // spgPublishedTabulations. Null when the tabulation does not exist or is older than the retention window.
    Task<PublishedTabulationsSource?> GetPublishedTabulationsAsync(Guid tabulationId, CancellationToken cancellationToken = default);

    // spgAssessmentScoreByTestId / spgPublishedAssessmentScoreByTestId.
    Task<AssessmentScoreSource?> GetAssessmentScoresAsync(Guid testId, Guid tabulationId, bool isPublished, CancellationToken cancellationToken = default);
}
