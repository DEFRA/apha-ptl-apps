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

    public async Task<MonthlyDistributionResponse?> GetScheduleAsync(int yearId, int monthId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<MonthlyDistributionResponse>(
            $"/api/distributions/months/{yearId}/{monthId}/schedule", cancellationToken);

    public async Task<MonthlyDistributionScheduleSaveResult?> SaveScheduleAsync(
        int yearId, int monthId, IReadOnlyList<MonthlyDistributionScheduleRowRequest> rows, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/distributions/months/{yearId}/{monthId}/schedule", rows, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MonthlyDistributionScheduleSaveResult>(cancellationToken);
    }
}
