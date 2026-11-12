namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // Nil Return reason currently stored against the distribution participant row.
    Task<ParticipantNilReturnData?> GetParticipantNilReturnAsync(
        Guid distributionParticipantId, CancellationToken cancellationToken = default);

    // Sets/clears nil-return reason on the participant row.
    Task SaveParticipantNilReturnReasonAsync(
        Guid distributionParticipantId, string nilReturnReason, CancellationToken cancellationToken = default);
}
