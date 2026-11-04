using System.Net.Http.Json;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public sealed partial class DistributionApiClient
{
    public async Task<ParticipantPastDistributionListResponse> GetParticipantDistributionHistoryAsync(
        Guid participantId, CancellationToken cancellationToken = default)
    {
        var history = await httpClient.GetFromJsonAsync<ParticipantPastDistributionListResponse>(
            $"/api/distributions/participants/{participantId}/history", cancellationToken);
        return history ?? new ParticipantPastDistributionListResponse([]);
    }

    public Task<ActualResultsResponse?> GetActualResultsForParticipantAsync(
        Guid monthlyDistributionSchemeId, Guid participantId, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<ActualResultsResponse>(
            $"/api/distributions/schemes/{monthlyDistributionSchemeId}/participant-results?participantId={participantId}", cancellationToken);
}
