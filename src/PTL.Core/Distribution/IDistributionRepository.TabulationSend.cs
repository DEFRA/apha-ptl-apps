namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // One transaction, as legacy MonthlyDistribution.Save -> each changed scheme's Update:
    // spuMonthlyDistributionSchemeForResultsReady, then sppSubmitComment (when SubmitComments),
    // then spuTabulationForSignOff for each tabulation whose publish state changes.
    Task SendTabulationSchemesAsync(IReadOnlyList<TabulationSendWrite> writes, CancellationToken cancellationToken = default);
}

public sealed record TabulationSendWrite(
    Guid MonthlyDistributionSchemeId,
    DateTime? IsReadyForTestConsultant,
    DateTime? IsReadyForResultsSignOff,
    DateTime? IsReadyForAssessor,
    bool SubmitComments,
    IReadOnlyList<(Guid TabulationId, int PublishState)> PublishStates);
