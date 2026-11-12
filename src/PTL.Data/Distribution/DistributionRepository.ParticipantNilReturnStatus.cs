using PTL.Core.Distribution;

namespace PTL.Data.Distribution;

public sealed partial class DistributionRepository
{
    public async Task<IReadOnlyList<SampleReceiptSampleEntity>> GetSampleReceiptRowsForParticipantAsync(
        Guid distributionParticipantId,
        Guid monthlyDistributionSchemeId,
        CancellationToken cancellationToken = default)
    {
        var data = await GetSampleReceiptAsync(distributionParticipantId, monthlyDistributionSchemeId, cancellationToken);
        return data?.Samples.ToList() ?? [];
    }
}
