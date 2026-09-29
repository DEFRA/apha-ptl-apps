using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Lookup;
using PTL.Contracts.WeightedPricingPlan;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

public class SystemAdministrationControllerTests
{
    private static SystemAdministrationController CreateController(
        FakeAdministrationChargeApiClient? administrationChargeApiClient = null,
        FakeWeightedPricingPlanApiClient? weightedPricingPlanApiClient = null,
        FakeLookupApiClient? lookupApiClient = null) =>
        new(
            administrationChargeApiClient ?? new FakeAdministrationChargeApiClient(),
            weightedPricingPlanApiClient ?? new FakeWeightedPricingPlanApiClient(),
            lookupApiClient ?? new FakeLookupApiClient(),
            NullLogger<SystemAdministrationController>.Instance);

    [Fact]
    public void SystemAdministration_ReturnsView()
    {
        var controller = CreateController();

        var result = controller.SystemAdministration();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task AdministrationCharge_Get_BuildsOneRowPerChargeWithACellPerCurrency()
    {
        var chargeId = Guid.NewGuid();
        var gbpId = Guid.NewGuid();
        var eurId = Guid.NewGuid();
        var lookupApiClient = new FakeLookupApiClient
        {
            Currencies = [new CurrencyResponse(gbpId, "British Pound", "£", "£ - British Pound"), new CurrencyResponse(eurId, "Euro", "€", "€ - Euro")]
        };
        var apiClient = new FakeAdministrationChargeApiClient
        {
            Charges = [new AdministrationChargeResponse(chargeId, "Administration Charge", [new AdministrationChargeCurrencyPriceResponse(gbpId, 25.00m)])]
        };
        var controller = CreateController(administrationChargeApiClient: apiClient, lookupApiClient: lookupApiClient);

        var result = await controller.AdministrationCharge(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdministrationChargeListViewModel>(view.Model);
        var charge = Assert.Single(model.Charges);
        Assert.Equal(2, charge.Prices.Count);
        Assert.Equal(25.00m, charge.Prices.Single(p => p.CurrencyId == gbpId).Price);
        Assert.Equal(0m, charge.Prices.Single(p => p.CurrencyId == eurId).Price);
    }

    [Fact]
    public async Task AdministrationCharge_Post_ValidModel_SavesEveryCellAndRedirects()
    {
        var chargeId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var apiClient = new FakeAdministrationChargeApiClient();
        var controller = CreateController(administrationChargeApiClient: apiClient);
        var model = new AdministrationChargeListViewModel
        {
            Charges =
            [
                new AdministrationChargeRowViewModel
                {
                    AdministrationChargeId = chargeId,
                    Name = "Administration Charge",
                    Prices = [new AdministrationChargePriceCellViewModel { CurrencyId = currencyId, CurrencyLabel = "£ - British Pound", Price = 30.00m }]
                }
            ]
        };

        var result = await controller.AdministrationCharge(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var request = Assert.Single(apiClient.SetPriceRequests);
        Assert.Equal(chargeId, request.AdministrationChargeId);
        Assert.Equal(30.00m, request.Price);
    }

    [Fact]
    public async Task AdministrationCharge_Post_InvalidModel_ReturnsViewWithoutSaving()
    {
        var apiClient = new FakeAdministrationChargeApiClient();
        var controller = CreateController(administrationChargeApiClient: apiClient);
        var model = new AdministrationChargeListViewModel
        {
            Charges =
            [
                new AdministrationChargeRowViewModel
                {
                    AdministrationChargeId = Guid.NewGuid(),
                    Name = "Administration Charge",
                    Prices = [new AdministrationChargePriceCellViewModel { CurrencyId = Guid.NewGuid(), CurrencyLabel = "£ - British Pound", Price = -1.00m }]
                }
            ]
        };
        controller.ModelState.AddModelError("Charges[0].Prices[0].Price", "Price must not be negative");

        var result = await controller.AdministrationCharge(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Empty(apiClient.SetPriceRequests);
    }

    [Fact]
    public async Task AdministrationCharge_Post_ApiSaveFails_ReturnsViewWithError()
    {
        var apiClient = new FakeAdministrationChargeApiClient { SetPriceSucceeds = false };
        var controller = CreateController(administrationChargeApiClient: apiClient);
        var model = new AdministrationChargeListViewModel
        {
            Charges =
            [
                new AdministrationChargeRowViewModel
                {
                    AdministrationChargeId = Guid.NewGuid(),
                    Name = "Administration Charge",
                    Prices = [new AdministrationChargePriceCellViewModel { CurrencyId = Guid.NewGuid(), CurrencyLabel = "£ - British Pound", Price = 30.00m }]
                }
            ]
        };

        var result = await controller.AdministrationCharge(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task WeightedPricingPlan_NoYearsConfigured_ShowsBannerInsteadOfGrid()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient { Years = new WeightedPricingPlanYearsResponse([], false, null, null) };
        var controller = CreateController(weightedPricingPlanApiClient: apiClient);

        var result = await controller.WeightedPricingPlan(null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.True(model.HasNoYears);
    }

    [Fact]
    public async Task WeightedPricingPlan_NoYearIdRequested_DefaultsToCurrentFinancialYearWhenItHasAPlan()
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
        var controller = CreateController(weightedPricingPlanApiClient: apiClient, lookupApiClient: lookupApiClient);

        var result = await controller.WeightedPricingPlan(null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.Equal(2026, model.SelectedYearId);
        var row = Assert.Single(model.Rows);
        Assert.Equal(50, row.WeightByDistributionsChosen[6]);
    }

    [Fact]
    public async Task WeightedPricingPlan_CurrentYearHasNoPlan_FallsBackToEarliestAvailableYear()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse([new YearResponse(2024, "2024/25")], false, null, null)
        };
        var lookupApiClient = new FakeLookupApiClient { Years = [new YearResponse(2026, "2026/27"), new YearResponse(2027, "2027/28")] };
        var controller = CreateController(weightedPricingPlanApiClient: apiClient, lookupApiClient: lookupApiClient);

        var result = await controller.WeightedPricingPlan(null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.Equal(2024, model.SelectedYearId);
    }

    [Fact]
    public async Task WeightedPricingPlan_ExplicitYearId_OverridesDefault()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse([new YearResponse(2024, "2024/25"), new YearResponse(2025, "2025/26")], false, null, null)
        };
        var controller = CreateController(weightedPricingPlanApiClient: apiClient);

        var result = await controller.WeightedPricingPlan(2025, CancellationToken.None);

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
        var controller = CreateController(weightedPricingPlanApiClient: apiClient);

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
        var controller = CreateController(weightedPricingPlanApiClient: apiClient);

        var result = await controller.Renew(2025, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Contains("2026/27", model.Message);
        Assert.Equal(1, apiClient.RenewCallCount);
    }
}

