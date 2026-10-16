using System.Net.Http.Json;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public partial interface IDistributionApiClient
{
    Task<TabulationsResponse?> PreviewTabulationsAsync(int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default);

    Task<TabulationSendResponse?> StageTabulationSendAsync(int yearId, int monthId, TabulationStageRequest request, CancellationToken cancellationToken = default);

    // False when a staged send is no longer available.
    Task<bool> SaveTabulationsAsync(int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default);
}

public sealed partial class DistributionApiClient
{
    public async Task<TabulationsResponse?> PreviewTabulationsAsync(
        int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/distributions/months/{yearId}/{monthId}/tabulations/preview", new TabulationsStagedRequest(staged), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TabulationsResponse>(cancellationToken);
    }

    public async Task<TabulationSendResponse?> StageTabulationSendAsync(
        int yearId, int monthId, TabulationStageRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/distributions/months/{yearId}/{monthId}/tabulations/stage", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TabulationSendResponse>(cancellationToken);
    }

    public async Task<bool> SaveTabulationsAsync(
        int yearId, int monthId, IReadOnlyList<TabulationStagedSendRequest> staged, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/distributions/months/{yearId}/{monthId}/tabulations", new TabulationsStagedRequest(staged), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
