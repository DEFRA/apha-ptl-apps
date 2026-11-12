namespace PTL.Core.Distribution;

public interface IParticipantNilReturnService
{
    Task<ParticipantNilReturnData?> GetAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<ParticipantNilReturnSaveOutcome> SaveAsync(
        Guid distributionParticipantId,
        Guid monthlyDistributionSchemeId,
        string? nilReturnReason,
        CancellationToken cancellationToken = default);
}

public sealed class ParticipantNilReturnService(
    IDistributionRepository distributionRepository,
    IActualResultsService actualResultsService,
    TimeProvider timeProvider) : IParticipantNilReturnService
{
    private const int ReasonMaxLength = 100;

    public async Task<ParticipantNilReturnData?> GetAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var actual = await actualResultsService.GetActualResultsAsync(distributionParticipantId, monthlyDistributionSchemeId, cancellationToken);
        if (actual is null)
        {
            return null;
        }

        var nilReturn = await distributionRepository.GetParticipantNilReturnAsync(distributionParticipantId, cancellationToken);
        return nilReturn ?? new ParticipantNilReturnData(distributionParticipantId, string.Empty);
    }

    public async Task<ParticipantNilReturnSaveOutcome> SaveAsync(
        Guid distributionParticipantId,
        Guid monthlyDistributionSchemeId,
        string? nilReturnReason,
        CancellationToken cancellationToken = default)
    {
        var actual = await actualResultsService.GetActualResultsAsync(distributionParticipantId, monthlyDistributionSchemeId, cancellationToken);
        if (actual is null)
        {
            return new ParticipantNilReturnSaveOutcome(ParticipantNilReturnSaveStatus.NotFound);
        }

        // Reuse the same effective close state as Results Entry.
        if (actual.IsReadOnly)
        {
            return new ParticipantNilReturnSaveOutcome(ParticipantNilReturnSaveStatus.DeadlinePassed);
        }

        if (actual.Tests.SelectMany(t => t.Samples).SelectMany(s => s.Values).Any(v => !string.IsNullOrWhiteSpace(v.Result))
            || actual.Tests.SelectMany(t => t.MethodItems).Any(m => !string.IsNullOrWhiteSpace(m.Result)))
        {
            return new ParticipantNilReturnSaveOutcome(ParticipantNilReturnSaveStatus.HasResults);
        }

        var reason = (nilReturnReason ?? string.Empty).Trim();
        if (reason.Length == 0)
        {
            return new ParticipantNilReturnSaveOutcome(ParticipantNilReturnSaveStatus.ReasonRequired);
        }

        if (reason.Length > ReasonMaxLength)
        {
            return new ParticipantNilReturnSaveOutcome(ParticipantNilReturnSaveStatus.TooLong);
        }

        await distributionRepository.SaveParticipantNilReturnReasonAsync(distributionParticipantId, reason, cancellationToken);

        var sampleRows = await distributionRepository.GetSampleReceiptRowsForParticipantAsync(
            distributionParticipantId,
            monthlyDistributionSchemeId,
            cancellationToken);

        if (sampleRows.Count > 0)
        {
            var today = timeProvider.GetLocalNow().Date;
            foreach (var sample in sampleRows)
            {
                sample.SubmissionDate = null;
                sample.NonCapableDate = null;
                sample.NonSubmissionDate ??= today;
            }

            await distributionRepository.UpdateSampleReceiptAsync(sampleRows, cancellationToken);
        }

        return new ParticipantNilReturnSaveOutcome(ParticipantNilReturnSaveStatus.Saved);
    }
}
