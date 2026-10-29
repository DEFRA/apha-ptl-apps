namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // Legacy spgCurrentDistributionsBySsoId semantics, adapted to participantId (already resolved
    // by External auth flow): posted, not-cancelled, effective deadline still open, and within the
    // seven-year retention window.
    Task<IReadOnlyList<CurrentDistributionData>> GetCurrentDistributionsAsync(Guid participantId, CancellationToken cancellationToken = default);
}
