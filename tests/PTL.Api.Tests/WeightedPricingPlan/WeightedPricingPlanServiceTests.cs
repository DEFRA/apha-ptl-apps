using PTL.Core.Lookup;
using PTL.Core.WeightedPricingPlan;

namespace PTL.Api.Tests.WeightedPricingPlan;

public class WeightedPricingPlanServiceTests
{
    private static WeightedPricingPlanService CreateService(FakeWeightedPricingPlanRepository repository, FakeLookupServiceForWeightedPricingPlan? lookupService = null) =>
        new(repository, lookupService ?? new FakeLookupServiceForWeightedPricingPlan());

    [Fact]
    public async Task GetYearsAsync_CurrentYearHasPlanAndNextDoesNot_CanRenewIsTrue()
    {
        var repository = new FakeWeightedPricingPlanRepository { YearsWithPercentages = [new YearEntity { YearId = 2026, Year = "2026/27" }] };
        var lookupService = new FakeLookupServiceForWeightedPricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var service = CreateService(repository, lookupService);

        var result = await service.GetYearsAsync();

        Assert.True(result.CanRenew);
        Assert.Equal(2027, result.NextYearId);
        Assert.Equal("2027/28", result.NextYearLabel);
    }

    [Fact]
    public async Task GetYearsAsync_NextYearAlreadyHasPlan_CanRenewIsFalse()
    {
        var repository = new FakeWeightedPricingPlanRepository
        {
            YearsWithPercentages = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var lookupService = new FakeLookupServiceForWeightedPricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var service = CreateService(repository, lookupService);

        var result = await service.GetYearsAsync();

        Assert.False(result.CanRenew);
    }

    [Fact]
    public async Task GetYearsAsync_CurrentYearHasNoPlan_CanRenewIsFalse()
    {
        var repository = new FakeWeightedPricingPlanRepository { YearsWithPercentages = [] };
        var lookupService = new FakeLookupServiceForWeightedPricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var service = CreateService(repository, lookupService);

        var result = await service.GetYearsAsync();

        Assert.False(result.CanRenew);
    }

    [Fact]
    public async Task GetPercentagesForYearAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeWeightedPricingPlanRepository
        {
            PercentagesByYear = new Dictionary<int, IReadOnlyList<PricingPercentageEntity>>
            {
                [2026] = [new PricingPercentageEntity { PricingPercentageId = Guid.NewGuid(), YearId = 2026, NumberOfDistributionsOnScheme = 12, NumberOfDistributionsChosen = 6, Weight = 50 }]
            }
        };
        var service = CreateService(repository);

        var result = await service.GetPercentagesForYearAsync(2026);

        Assert.Single(result);
        Assert.Equal(50, result[0].Weight);
    }

    [Fact]
    public async Task RenewAsync_Eligible_RenewsAndReturnsSuccessMessage()
    {
        var repository = new FakeWeightedPricingPlanRepository { YearsWithPercentages = [new YearEntity { YearId = 2026, Year = "2026/27" }] };
        var lookupService = new FakeLookupServiceForWeightedPricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var service = CreateService(repository, lookupService);

        var result = await service.RenewAsync();

        Assert.True(result.Success);
        Assert.Contains("2027/28", result.Message);
        Assert.Equal(2027, Assert.Single(repository.RenewedYearIds));
    }

    [Fact]
    public async Task RenewAsync_NotEligible_DoesNotRenewAndReturnsExplanatoryMessage()
    {
        var repository = new FakeWeightedPricingPlanRepository { YearsWithPercentages = [] };
        var lookupService = new FakeLookupServiceForWeightedPricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var service = CreateService(repository, lookupService);

        var result = await service.RenewAsync();

        Assert.False(result.Success);
        Assert.NotEmpty(result.Message);
        Assert.Empty(repository.RenewedYearIds);
    }
}
