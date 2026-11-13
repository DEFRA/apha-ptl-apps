using System.Net.Http.Json;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public sealed partial class DistributionApiClient
{
    public async Task<ParticipantNilReturnResponse?> GetParticipantNilReturnAsync(
        Guid monthlyDistributionSchemeId,
        Guid distributionParticipantId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            new Uri($"/api/distributions/schemes/{monthlyDistributionSchemeId}/participants/{distributionParticipantId}/nil-return", UriKind.Relative),
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantNilReturnResponse>(cancellationToken);
    }

    public async Task<ParticipantNilReturnSaveResult?> SaveParticipantNilReturnAsync(
        Guid monthlyDistributionSchemeId,
        Guid distributionParticipantId,
        ParticipantNilReturnSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/distributions/schemes/{monthlyDistributionSchemeId}/participants/{distributionParticipantId}/nil-return",
            request,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantNilReturnSaveResult>(cancellationToken);
    }
}
