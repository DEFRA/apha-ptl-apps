using PTL.Core.Participant;

namespace PTL.Core.Distribution;

// Legacy ExternalWeb DistributionsPrevious.aspx / ResultsView.aspx - kept out of
// IDistributionService/DistributionService (whose constructor is shared by many unrelated
// InternalWeb features) since this is the only consumer that needs IParticipantRepository.
public interface IParticipantDistributionHistoryService
{
    Task<IReadOnlyList<ParticipantPastDistributionEntity>> GetParticipantDistributionHistoryAsync(
        Guid participantId, CancellationToken cancellationToken = default);

    // Legacy ResultsView.aspx only ever knows the signed-in participant and a distribution id
    // (?ref={MonthlyDistributionSchemeId}) - resolves the participant's own
    // DistributionParticipantId first (spgResultsEntry's own @ActualResultsId lookup), so a
    // participant can never be handed someone else's results (Access Restrictions AC). Null when
    // this participant has no row for that distribution.
    Task<ActualResultsPage?> GetActualResultsForParticipantAsync(
        Guid participantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);
}

public sealed class ParticipantDistributionHistoryService(
    IDistributionRepository distributionRepository,
    IParticipantRepository participantRepository,
    IActualResultsService actualResultsService) : IParticipantDistributionHistoryService
{
    // spgPastDistributionsBySsoId takes the participant's SsoId, not their internal ParticipantId -
    // same resolve-then-call pattern as SchemeService.GetSchemesForParticipantAsync.
    public async Task<IReadOnlyList<ParticipantPastDistributionEntity>> GetParticipantDistributionHistoryAsync(
        Guid participantId, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        return participant is null
            ? []
            : await distributionRepository.GetParticipantDistributionHistoryAsync(participant.SsoId, cancellationToken);
    }

    public async Task<ActualResultsPage?> GetActualResultsForParticipantAsync(
        Guid participantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return null;
        }

        var distributionParticipantId = await distributionRepository.ResolveDistributionParticipantIdAsync(
            participant.SsoId, monthlyDistributionSchemeId, cancellationToken);

        return distributionParticipantId is null
            ? null
            : await actualResultsService.GetActualResultsAsync(distributionParticipantId.Value, monthlyDistributionSchemeId, cancellationToken);
    }
}
