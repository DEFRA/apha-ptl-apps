namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // spgPastDistributionsBySsoId - legacy ExternalWeb DistributionsPrevious.aspx
    // (PastDistributionCollection.FetchPastDistributionCollection) - every distribution for which
    // results have been posted back to this participant, across all schemes and years.
    Task<IReadOnlyList<ParticipantPastDistributionEntity>> GetParticipantDistributionHistoryAsync(
        Guid ssoId, CancellationToken cancellationToken = default);

    // spgResultsEntry's own @ActualResultsId resolution (not cancelled, within the 7-year
    // retention window) - resolves a participant's own DistributionParticipantId for a given
    // distribution, so the read-only Results screen never needs the caller to supply someone
    // else's id. Null when this participant has no row for that distribution (Access Restrictions
    // AC) - the procedure's header SELECT then returns zero rows.
    Task<Guid?> ResolveDistributionParticipantIdAsync(
        Guid ssoId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);
}
