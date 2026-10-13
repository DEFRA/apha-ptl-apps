using PTL.Core.Lookup;
using PTL.Core.PostagePricingPlan;

namespace PTL.Api.Tests.PostagePricingPlan;

public class PostagePricingPlanServiceTests
{
    private static PostagePricingPlanService CreateService(FakePostagePricingPlanRepository repository, FakeLookupServiceForPostagePricingPlan? lookupService = null) =>
        new(repository, lookupService ?? new FakeLookupServiceForPostagePricingPlan());

    [Fact]
    public async Task GetYearsAsync_CurrentYearHasPlanAndNextDoesNot_CanRenewIsTrue()
    {
        var repository = new FakePostagePricingPlanRepository { YearsWithPlan = [new YearEntity { YearId = 2026, Year = "2026/27" }] };
        var lookupService = new FakeLookupServiceForPostagePricingPlan
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
        var repository = new FakePostagePricingPlanRepository
        {
            YearsWithPlan = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var lookupService = new FakeLookupServiceForPostagePricingPlan
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
        var repository = new FakePostagePricingPlanRepository { YearsWithPlan = [] };
        var lookupService = new FakeLookupServiceForPostagePricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var service = CreateService(repository, lookupService);

        var result = await service.GetYearsAsync();

        Assert.False(result.CanRenew);
    }

    // Covers the early-return branch: spgaYearCurrent hasn't been configured with both a current
    // and next financial year (e.g. mid system-setup), so there's nothing to compare against and
    // renewal must be reported as unavailable rather than throwing on currentYears[1].
    [Fact]
    public async Task GetYearsAsync_FewerThanTwoCurrentYearsConfigured_ReturnsCannotRenewWithNoNextYear()
    {
        var repository = new FakePostagePricingPlanRepository { YearsWithPlan = [new YearEntity { YearId = 2026, Year = "2026/27" }] };
        var lookupService = new FakeLookupServiceForPostagePricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }]
        };
        var service = CreateService(repository, lookupService);

        var result = await service.GetYearsAsync();

        Assert.False(result.CanRenew);
        Assert.Null(result.NextYearId);
        Assert.Null(result.NextYearLabel);
        Assert.Same(repository.YearsWithPlan, result.AvailableYears);
    }

    [Fact]
    public async Task SetPriceAsync_ValidPrices_SavesAndReturnsValidResult()
    {
        var postageId = Guid.NewGuid();
        var repository = new FakePostagePricingPlanRepository
        {
            ExistingById = new Dictionary<Guid, PostagePricingPlanEntity> { [postageId] = new() { PostageId = postageId, Name = "Courier", YearId = 2026 } }
        };
        var service = CreateService(repository);

        var result = await service.SetPriceAsync(postageId, 5.00m, 10.00m, 15.00m);

        Assert.True(result.IsValid);
        var saved = Assert.Single(repository.SavedPrices);
        Assert.Equal(postageId, saved.PostageId);
        Assert.Equal(2026, saved.YearId);
        Assert.Equal(5.00m, saved.UKPrice);
        Assert.Equal(10.00m, saved.EUPrice);
        Assert.Equal(15.00m, saved.NonEUPrice);
    }

    [Fact]
    public async Task SetPriceAsync_NegativePrice_ReturnsErrorAndDoesNotPersist()
    {
        var postageId = Guid.NewGuid();
        var repository = new FakePostagePricingPlanRepository
        {
            ExistingById = new Dictionary<Guid, PostagePricingPlanEntity> { [postageId] = new() { PostageId = postageId, Name = "Courier", YearId = 2026 } }
        };
        var service = CreateService(repository);

        var result = await service.SetPriceAsync(postageId, -1.00m, 10.00m, 15.00m);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "UKPrice");
        Assert.Empty(repository.SavedPrices);
    }

    // The two sibling features (AdministrationCharge/WeightedPricingPlan) never let a client choose
    // which row to mutate by a stale/tampered id - this is the equivalent guard for Postage: a
    // PostageId that doesn't exist must fail, not silently "succeed" with nothing persisted.
    [Fact]
    public async Task SetPriceAsync_UnknownPostageId_ReturnsNotFoundErrorAndDoesNotPersist()
    {
        var repository = new FakePostagePricingPlanRepository();
        var service = CreateService(repository);

        var result = await service.SetPriceAsync(Guid.NewGuid(), 5.00m, 10.00m, 15.00m);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "PostageId");
        Assert.Empty(repository.SavedPrices);
    }

    [Fact]
    public async Task RenewAsync_Eligible_RenewsAndReturnsSuccessMessage()
    {
        var repository = new FakePostagePricingPlanRepository { YearsWithPlan = [new YearEntity { YearId = 2026, Year = "2026/27" }] };
        var lookupService = new FakeLookupServiceForPostagePricingPlan
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
        var repository = new FakePostagePricingPlanRepository { YearsWithPlan = [] };
        var lookupService = new FakeLookupServiceForPostagePricingPlan
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
