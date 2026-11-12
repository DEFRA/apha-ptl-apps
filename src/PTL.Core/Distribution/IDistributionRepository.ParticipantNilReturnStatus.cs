namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // Reads the participant's sample receipt rows for the monthly scheme, used to stamp
    // Not Submitted when an external participant records a Nil Return.
    Task<IReadOnlyList<SampleReceiptSampleEntity>> GetSampleReceiptRowsForParticipantAsync(
        Guid distributionParticipantId,
        Guid monthlyDistributionSchemeId,
        CancellationToken cancellationToken = default);
}
