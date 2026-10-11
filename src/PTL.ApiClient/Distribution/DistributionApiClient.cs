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

    public async Task<SampleNumbersPageResult> GetSampleNumbersAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            new Uri($"/api/distributions/schemes/{monthlyDistributionSchemeId}/sample-numbers", UriKind.Relative), cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new SampleNumbersPageResult(null, []);
        }

        // The structure could not be built from the Scheme - surface the business message rather
        // than failing with an opaque error.
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.SelectMany(e => e.Value).ToList()
                : ["Sample Numbers could not be opened for this distribution."];
            return new SampleNumbersPageResult(null, errors);
        }

        response.EnsureSuccessStatusCode();
        return new SampleNumbersPageResult(await response.Content.ReadFromJsonAsync<SampleNumbersResponse>(cancellationToken), []);
    }

    public async Task<SampleNumbersSaveResult?> SaveSampleNumbersAsync(
        Guid monthlyDistributionSchemeId, SampleNumbersSaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/distributions/schemes/{monthlyDistributionSchemeId}/sample-numbers", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SampleNumbersSaveResult>(cancellationToken);
    }
}
