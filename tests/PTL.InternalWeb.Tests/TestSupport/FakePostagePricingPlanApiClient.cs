using PTL.ApiClient;
using PTL.Contracts.PostagePricingPlan;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IPostagePricingPlanApiClient so SystemAdministrationController tests don't need
// a real HTTP call to PTL.Api.
internal sealed class FakePostagePricingPlanApiClient : IPostagePricingPlanApiClient
{
    public PostagePricingPlanYearsResponse Years { get; set; } = new([], false, null, null);
    public PostagePricingPlanSaveResult SaveResult { get; set; } = new(true, new Dictionary<string, string[]>());
    public PostagePricingPlanRenewResponse RenewResponse { get; set; } = new(true, "Renewed.");
    public UpdatePostagePricingPlanPriceRequest? LastSetPriceRequest { get; private set; }
    public int RenewCallCount { get; private set; }

    public Task<PostagePricingPlanYearsResponse> GetYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Years);

    public Task<PostagePricingPlanSaveResult> SetPriceAsync(UpdatePostagePricingPlanPriceRequest request, CancellationToken cancellationToken = default)
    {
        LastSetPriceRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<PostagePricingPlanRenewResponse> RenewAsync(CancellationToken cancellationToken = default)
    {
        RenewCallCount++;
        return Task.FromResult(RenewResponse);
    }
}
