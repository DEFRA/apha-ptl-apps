using System.Net.Http.Json;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

// Thin typed HttpClient wrapper around PTL.Api's Distribution Dashboard endpoints.
public sealed class DistributionApiClient(HttpClient httpClient) : IDistributionApiClient
{
    public async Task<IReadOnlyList<DistributionDashboardMonthResponse>> GetDashboardAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var months = await httpClient.GetFromJsonAsync<IReadOnlyList<DistributionDashboardMonthResponse>>(
            $"/api/distributions/dashboard?year={yearId}", cancellationToken);
        return months ?? [];
    }

    public async Task<IReadOnlyList<DistributionYearOptionResponse>> GetYearsAsync(CancellationToken cancellationToken = default)
    {
        var years = await httpClient.GetFromJsonAsync<IReadOnlyList<DistributionYearOptionResponse>>(
            "/api/distributions/years", cancellationToken);
        return years ?? [];
    }
}
