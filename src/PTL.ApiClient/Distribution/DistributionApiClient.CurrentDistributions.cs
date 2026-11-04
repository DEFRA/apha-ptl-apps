using System.Net.Http.Json;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public partial interface IDistributionApiClient
{
    Task<IReadOnlyList<CurrentDistributionResponse>> GetCurrentDistributionsAsync(Guid participantId, CancellationToken cancellationToken = default);
}

public sealed partial class DistributionApiClient
{
    public async Task<IReadOnlyList<CurrentDistributionResponse>> GetCurrentDistributionsAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<IReadOnlyList<CurrentDistributionResponse>>(
            $"/api/distributions/participants/{participantId}/current", cancellationToken);
        return response ?? [];
    }
}
