using PTL.ApiClient;
using PTL.Contracts.WeightedPricingPlan;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IWeightedPricingPlanApiClient so WeightedPricingPlanController tests don't need
// a real HTTP call to PTL.Api.
internal sealed class FakeWeightedPricingPlanApiClient : IWeightedPricingPlanApiClient
{
    public WeightedPricingPlanYearsResponse Years { get; set; } = new([], false, null, null);
    public Dictionary<int, IReadOnlyList<PricingPercentageResponse>> PercentagesByYear { get; set; } = [];
    public WeightedPricingPlanRenewResponse RenewResponse { get; set; } = new(true, "Renewed.");
    public int RenewCallCount { get; private set; }

    public Task<WeightedPricingPlanYearsResponse> GetYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Years);

    public Task<IReadOnlyList<PricingPercentageResponse>> GetPercentagesForYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PercentagesByYear.GetValueOrDefault(yearId, []));

    public Task<WeightedPricingPlanRenewResponse> RenewAsync(CancellationToken cancellationToken = default)
    {
        RenewCallCount++;
        return Task.FromResult(RenewResponse);
    }
}
