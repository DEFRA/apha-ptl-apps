namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // spgTabulations - legacy ExternalWeb Tabulations.aspx (TabulationCollection.
    // FetchTabulationCollection) - every published tabulation this SsoId is authorised to view,
    // across all schemes and years.
    Task<IReadOnlyList<PublishedTabulationSummaryEntity>> GetTabulationsForSsoIdAsync(
        Guid ssoId, CancellationToken cancellationToken = default);
}
