using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public partial interface IDistributionApiClient
{
    // Legacy ExternalWeb DistributionsPrevious.aspx - every distribution posted to this participant.
    Task<ParticipantPastDistributionListResponse> GetParticipantDistributionHistoryAsync(
        Guid participantId, CancellationToken cancellationToken = default);

    // Legacy ExternalWeb ResultsView.aspx - read-only, resolved against the signed-in participant
    // server-side. Null when this participant has no results for that distribution.
    Task<ActualResultsResponse?> GetActualResultsForParticipantAsync(
        Guid monthlyDistributionSchemeId, Guid participantId, CancellationToken cancellationToken = default);
}
