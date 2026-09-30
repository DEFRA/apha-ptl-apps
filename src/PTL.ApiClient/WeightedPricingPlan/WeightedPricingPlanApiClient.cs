using System.Net.Http.Json;
using PTL.Contracts.WeightedPricingPlan;

namespace PTL.ApiClient;

public interface IWeightedPricingPlanApiClient
{
    Task<WeightedPricingPlanYearsResponse> GetYearsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PricingPercentageResponse>> GetPercentagesForYearAsync(int yearId, CancellationToken cancellationToken = default);
    Task<WeightedPricingPlanRenewResponse> RenewAsync(CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's weighted pricing plan endpoints, shared by every
// web front-end (PTL.InternalWeb, PTL.ExternalWeb, ...).
public sealed class WeightedPricingPlanApiClient(HttpClient httpClient) : IWeightedPricingPlanApiClient
{
    public async Task<WeightedPricingPlanYearsResponse> GetYearsAsync(CancellationToken cancellationToken = default)
    {
        var years = await httpClient.GetFromJsonAsync<WeightedPricingPlanYearsResponse>("/api/weighted-pricing-plan/years", cancellationToken);
        return years ?? new WeightedPricingPlanYearsResponse([], false, null, null);
    }

    public async Task<IReadOnlyList<PricingPercentageResponse>> GetPercentagesForYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var percentages = await httpClient.GetFromJsonAsync<IReadOnlyList<PricingPercentageResponse>>($"/api/weighted-pricing-plan?yearId={yearId}", cancellationToken);
        return percentages ?? [];
    }

    public async Task<WeightedPricingPlanRenewResponse> RenewAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync("/api/weighted-pricing-plan/renew", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WeightedPricingPlanRenewResponse>(cancellationToken);
        return result ?? new WeightedPricingPlanRenewResponse(false, "The renew request did not return a result.");
    }
}
