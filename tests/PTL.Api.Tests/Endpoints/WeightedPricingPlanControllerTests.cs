using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Api.Tests.WeightedPricingPlan;
using PTL.Core.Lookup;
using PTL.Core.WeightedPricingPlan;

namespace PTL.Api.Tests.Endpoints;

public class WeightedPricingPlanControllerTests
{
    private static WeightedPricingPlanController CreateController(FakeWeightedPricingPlanRepository repository, FakeLookupServiceForWeightedPricingPlan? lookupService = null) =>
        new(new WeightedPricingPlanService(repository, lookupService ?? new FakeLookupServiceForWeightedPricingPlan()));

    [Fact]
    public async Task GetYears_ReturnsMappedYearsAndRenewEligibility()
    {
        var repository = new FakeWeightedPricingPlanRepository { YearsWithPercentages = [new YearEntity { YearId = 2026, Year = "2026/27" }] };
        var lookupService = new FakeLookupServiceForWeightedPricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var controller = CreateController(repository, lookupService);

        var result = await controller.GetYears(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PTL.Contracts.WeightedPricingPlan.WeightedPricingPlanYearsResponse>(ok.Value);
        Assert.True(response.CanRenew);
        Assert.Equal(2027, response.NextYearId);
        Assert.Single(response.AvailableYears);
    }

    [Fact]
    public async Task GetPercentages_ReturnsMappedPercentages()
    {
        var repository = new FakeWeightedPricingPlanRepository
        {
            PercentagesByYear = new Dictionary<int, IReadOnlyList<PricingPercentageEntity>>
            {
                [2026] = [new PricingPercentageEntity { PricingPercentageId = Guid.NewGuid(), YearId = 2026, NumberOfDistributionsOnScheme = 12, NumberOfDistributionsChosen = 6, Weight = 50 }]
            }
        };
        var controller = CreateController(repository);

        var result = await controller.GetPercentages(2026, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<List<PTL.Contracts.WeightedPricingPlan.PricingPercentageResponse>>(ok.Value);
        Assert.Equal(50, Assert.Single(response).Weight);
    }

    [Fact]
    public async Task Renew_NotEligible_ReturnsUnsuccessfulResponse()
    {
        var repository = new FakeWeightedPricingPlanRepository { YearsWithPercentages = [] };
        var lookupService = new FakeLookupServiceForWeightedPricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var controller = CreateController(repository, lookupService);

        var result = await controller.Renew(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PTL.Contracts.WeightedPricingPlan.WeightedPricingPlanRenewResponse>(ok.Value);
        Assert.False(response.Success);
        Assert.NotEmpty(response.Message);
    }

    [Fact]
    public async Task Renew_Eligible_RenewsAndReturnsSuccessfulResponse()
    {
        var repository = new FakeWeightedPricingPlanRepository { YearsWithPercentages = [new YearEntity { YearId = 2026, Year = "2026/27" }] };
        var lookupService = new FakeLookupServiceForWeightedPricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var controller = CreateController(repository, lookupService);

        var result = await controller.Renew(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PTL.Contracts.WeightedPricingPlan.WeightedPricingPlanRenewResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(repository.RenewedYearIds);
    }
}
