using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Lookup;
using PTL.Contracts.WeightedPricingPlan;
using PTL.InternalWeb.Features.WeightedPricingPlan;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.WeightedPricingPlan;

public class WeightedPricingPlanControllerTests
{
    private static WeightedPricingPlanController CreateController(FakeWeightedPricingPlanApiClient apiClient, FakeLookupApiClient? lookupApiClient = null) =>
        new(apiClient, lookupApiClient ?? new FakeLookupApiClient(), NullLogger<WeightedPricingPlanController>.Instance);

    [Fact]
    public async Task Index_NoYearsConfigured_ShowsBannerInsteadOfGrid()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient { Years = new WeightedPricingPlanYearsResponse([], false, null, null) };
        var controller = CreateController(apiClient);

        var result = await controller.Index(null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.True(model.HasNoYears);
    }

    [Fact]
    public async Task Index_NoYearIdRequested_DefaultsToCurrentFinancialYearWhenItHasAPlan()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse([new YearResponse(2025, "2025/26"), new YearResponse(2026, "2026/27")], true, 2027, "2027/28"),
            PercentagesByYear = new Dictionary<int, IReadOnlyList<PricingPercentageResponse>>
            {
                [2026] = [new PricingPercentageResponse(12, 6, 50)]
            }
        };
        var lookupApiClient = new FakeLookupApiClient { Years = [new YearResponse(2026, "2026/27"), new YearResponse(2027, "2027/28")] };
        var controller = CreateController(apiClient, lookupApiClient);

        var result = await controller.Index(null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.Equal(2026, model.SelectedYearId);
        var row = Assert.Single(model.Rows);
        Assert.Equal(50, row.WeightByDistributionsChosen[6]);
    }

    [Fact]
    public async Task Index_CurrentYearHasNoPlan_FallsBackToEarliestAvailableYear()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse([new YearResponse(2024, "2024/25")], false, null, null)
        };
        var lookupApiClient = new FakeLookupApiClient { Years = [new YearResponse(2026, "2026/27"), new YearResponse(2027, "2027/28")] };
        var controller = CreateController(apiClient, lookupApiClient);

        var result = await controller.Index(null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.Equal(2024, model.SelectedYearId);
    }

    [Fact]
    public async Task Index_ExplicitYearId_OverridesDefault()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse([new YearResponse(2024, "2024/25"), new YearResponse(2025, "2025/26")], false, null, null)
        };
        var controller = CreateController(apiClient);

        var result = await controller.Index(2025, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.Equal(2025, model.SelectedYearId);
    }

    [Fact]
    public async Task Renew_Blocked_RedisplaysWithErrorMessageAndSameYear()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse([new YearResponse(2025, "2025/26")], false, null, null),
            RenewResponse = new WeightedPricingPlanRenewResponse(false, "No weighted pricing percentages have been entered for the current financial year, so the plan cannot be renewed.")
        };
        var controller = CreateController(apiClient);

        var result = await controller.Renew(2025, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.True(model.MessageIsError);
        Assert.Equal(2025, model.SelectedYearId);
    }

    [Fact]
    public async Task Renew_Succeeds_RedisplaysWithSuccessMessage()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse([new YearResponse(2025, "2025/26")], false, null, null),
            RenewResponse = new WeightedPricingPlanRenewResponse(true, "The weighted pricing plan has successfully been renewed for the financial year 2026/27.")
        };
        var controller = CreateController(apiClient);

        var result = await controller.Renew(2025, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Contains("2026/27", model.Message);
        Assert.Equal(1, apiClient.RenewCallCount);
    }
}
