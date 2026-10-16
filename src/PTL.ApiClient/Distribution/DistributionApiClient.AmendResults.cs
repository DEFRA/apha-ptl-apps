using System.Net.Http.Json;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public partial interface IDistributionApiClient
{
    Task<AmendResultsResponse?> GetAmendResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    Task<AmendResultsSaveResult?> SaveAmendResultsAsync(Guid monthlyDistributionSchemeId, AmendResultsSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed partial class DistributionApiClient
{
    public Task<AmendResultsResponse?> GetAmendResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        GetOrNullAsync<AmendResultsResponse>($"/api/distributions/schemes/{monthlyDistributionSchemeId}/amend-results", cancellationToken);

    public async Task<AmendResultsSaveResult?> SaveAmendResultsAsync(
        Guid monthlyDistributionSchemeId, AmendResultsSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/distributions/schemes/{monthlyDistributionSchemeId}/amend-results", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AmendResultsSaveResult>(cancellationToken);
    }
}
