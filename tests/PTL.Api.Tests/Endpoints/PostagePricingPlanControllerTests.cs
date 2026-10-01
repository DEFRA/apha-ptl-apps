using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.PostagePricingPlan;
using PTL.Contracts.PostagePricingPlan;
using PTL.Core.Lookup;
using PTL.Core.PostagePricingPlan;

namespace PTL.Api.Tests.Endpoints;

public class PostagePricingPlanControllerTests
{
    private static PostagePricingPlanController CreateController(FakePostagePricingPlanRepository repository, FakeLookupServiceForPostagePricingPlan? lookupService = null)
    {
        var controller = new PostagePricingPlanController(new PostagePricingPlanService(repository, lookupService ?? new FakeLookupServiceForPostagePricingPlan()));

        // ValidationProblem() resolves ProblemDetailsFactory from HttpContext.RequestServices,
        // which a bare controller instance does not have without this wiring.
        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    [Fact]
    public async Task GetYears_ReturnsMappedYearsAndRenewEligibility()
    {
        var repository = new FakePostagePricingPlanRepository { YearsWithPlan = [new YearEntity { YearId = 2026, Year = "2026/27" }] };
        var lookupService = new FakeLookupServiceForPostagePricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var controller = CreateController(repository, lookupService);

        var result = await controller.GetYears(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PostagePricingPlanYearsResponse>(ok.Value);
        Assert.True(response.CanRenew);
        Assert.Equal(2027, response.NextYearId);
        Assert.Single(response.AvailableYears);
    }

    [Fact]
    public async Task SetPrice_ValidRequest_ReturnsOkAndPersists()
    {
        var postageId = Guid.NewGuid();
        var repository = new FakePostagePricingPlanRepository
        {
            ExistingById = new Dictionary<Guid, PostagePricingPlanEntity> { [postageId] = new() { PostageId = postageId, Name = "Courier", YearId = 2026 } }
        };
        var controller = CreateController(repository);

        var result = await controller.SetPrice(new UpdatePostagePricingPlanPriceRequest(postageId, 5m, 10m, 15m), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PostagePricingPlanSaveResult>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(repository.SavedPrices);
    }

    [Fact]
    public async Task SetPrice_NegativePrice_ReturnsValidationProblemAndDoesNotPersist()
    {
        var postageId = Guid.NewGuid();
        var repository = new FakePostagePricingPlanRepository
        {
            ExistingById = new Dictionary<Guid, PostagePricingPlanEntity> { [postageId] = new() { PostageId = postageId, Name = "Courier", YearId = 2026 } }
        };
        var controller = CreateController(repository);

        var result = await controller.SetPrice(new UpdatePostagePricingPlanPriceRequest(postageId, -1m, 10m, 15m), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(repository.SavedPrices);
    }

    [Fact]
    public async Task SetPrice_UnknownPostageId_ReturnsValidationProblemAndDoesNotPersist()
    {
        var repository = new FakePostagePricingPlanRepository();
        var controller = CreateController(repository);

        var result = await controller.SetPrice(new UpdatePostagePricingPlanPriceRequest(Guid.NewGuid(), 5m, 10m, 15m), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(repository.SavedPrices);
    }

    [Fact]
    public async Task Renew_NotEligible_ReturnsUnsuccessfulResponse()
    {
        var repository = new FakePostagePricingPlanRepository { YearsWithPlan = [] };
        var lookupService = new FakeLookupServiceForPostagePricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var controller = CreateController(repository, lookupService);

        var result = await controller.Renew(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PostagePricingPlanRenewResponse>(ok.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Renew_Eligible_RenewsAndReturnsSuccessfulResponse()
    {
        var repository = new FakePostagePricingPlanRepository { YearsWithPlan = [new YearEntity { YearId = 2026, Year = "2026/27" }] };
        var lookupService = new FakeLookupServiceForPostagePricingPlan
        {
            CurrentYears = [new YearEntity { YearId = 2026, Year = "2026/27" }, new YearEntity { YearId = 2027, Year = "2027/28" }]
        };
        var controller = CreateController(repository, lookupService);

        var result = await controller.Renew(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PostagePricingPlanRenewResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(repository.RenewedYearIds);
    }
}
