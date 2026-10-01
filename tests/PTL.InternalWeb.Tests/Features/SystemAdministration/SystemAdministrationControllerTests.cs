using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;
using PTL.Contracts.WeightedPricingPlan;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

public class SystemAdministrationControllerTests
{
    private static SystemAdministrationController CreateController(
        FakeAdministrationChargeApiClient? administrationChargeApiClient = null,
        FakeWeightedPricingPlanApiClient? weightedPricingPlanApiClient = null,
        FakePostagePricingPlanApiClient? postagePricingPlanApiClient = null,
        FakeLookupApiClient? lookupApiClient = null) =>
        new(
            administrationChargeApiClient ?? new FakeAdministrationChargeApiClient(),
            weightedPricingPlanApiClient ?? new FakeWeightedPricingPlanApiClient(),
            postagePricingPlanApiClient ?? new FakePostagePricingPlanApiClient(),
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
        var yearOptions = model.YearOptions.ToList();
        Assert.Equal(2, yearOptions.Count);
        Assert.Contains(yearOptions, o => o.Text == "2024/25" && o.Value == "2024");
        Assert.Contains(yearOptions, o => o.Text == "2025/26" && o.Value == "2025");
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

    [Fact]
    public async Task PostagePricingPlan_NoYearsConfigured_ShowsBannerInsteadOfGrid()
    {
        var apiClient = new FakePostagePricingPlanApiClient { Years = new PostagePricingPlanYearsResponse([], false, null, null) };
        var controller = CreateController(postagePricingPlanApiClient: apiClient);

        var result = await controller.PostagePricingPlan(null, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PostagePricingPlanViewModel>(view.Model);
        Assert.True(model.HasNoYears);
    }

    [Fact]
    public async Task PostagePricingPlan_NoYearIdRequested_DefaultsToCurrentFinancialYearWhenItHasAPlan()
    {
        var postageId = Guid.NewGuid();
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2025, "2025/26"), new YearResponse(2026, "2026/27")], true, 2027, "2027/28")
        };
        var lookupApiClient = new FakeLookupApiClient
        {
            Years = [new YearResponse(2026, "2026/27"), new YearResponse(2027, "2027/28")],
            PostagePricingPlans = [new PostagePricingPlanResponse(postageId, "Courier", 10m, 20m, 30m, 2026)]
        };
        var controller = CreateController(postagePricingPlanApiClient: apiClient, lookupApiClient: lookupApiClient);

        var result = await controller.PostagePricingPlan(null, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PostagePricingPlanViewModel>(view.Model);
        Assert.Equal(2026, model.SelectedYearId);
        var row = Assert.Single(model.Rows);
        Assert.Equal("Courier", row.Name);
        Assert.False(row.IsEditing);
    }

    [Fact]
    public async Task PostagePricingPlan_CurrentYearHasNoPlan_FallsBackToEarliestAvailableYear()
    {
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2024, "2024/25")], false, null, null)
        };
        var lookupApiClient = new FakeLookupApiClient { Years = [new YearResponse(2026, "2026/27"), new YearResponse(2027, "2027/28")] };
        var controller = CreateController(postagePricingPlanApiClient: apiClient, lookupApiClient: lookupApiClient);

        var result = await controller.PostagePricingPlan(null, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PostagePricingPlanViewModel>(view.Model);
        Assert.Equal(2024, model.SelectedYearId);
    }

    [Fact]
    public async Task PostagePricingPlan_EditIdMatchesRow_MarksThatRowAsEditingAndOthersReadOnly()
    {
        var editingId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2026, "2026/27")], false, null, null)
        };
        var lookupApiClient = new FakeLookupApiClient
        {
            Years = [new YearResponse(2026, "2026/27")],
            PostagePricingPlans =
            [
                new PostagePricingPlanResponse(editingId, "Biofreeze", 10m, 20m, 30m, 2026),
                new PostagePricingPlanResponse(otherId, "Courier", 5m, 15m, 25m, 2026)
            ]
        };
        var controller = CreateController(postagePricingPlanApiClient: apiClient, lookupApiClient: lookupApiClient);

        var result = await controller.PostagePricingPlan(2026, editingId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PostagePricingPlanViewModel>(view.Model);
        Assert.True(model.Rows.Single(r => r.PostageId == editingId).IsEditing);
        Assert.False(model.Rows.Single(r => r.PostageId == otherId).IsEditing);
    }

    [Fact]
    public async Task PostagePricingPlanSave_ValidModel_SavesAndRedirects()
    {
        var postageId = Guid.NewGuid();
        var apiClient = new FakePostagePricingPlanApiClient();
        var controller = CreateController(postagePricingPlanApiClient: apiClient);
        var model = new PostagePricingPlanEditViewModel { PostageId = postageId, YearId = 2026, UKPrice = 5m, EUPrice = 10m, NonEUPrice = 15m };

        var result = await controller.PostagePricingPlanSave(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.PostagePricingPlan), redirect.ActionName);
        Assert.Equal(postageId, apiClient.LastSetPriceRequest?.PostageId);
    }

    [Fact]
    public async Task PostagePricingPlanSave_InvalidModel_RedisplaysRowInEditModeWithoutSaving()
    {
        var postageId = Guid.NewGuid();
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2026, "2026/27")], false, null, null)
        };
        var lookupApiClient = new FakeLookupApiClient
        {
            Years = [new YearResponse(2026, "2026/27")],
            PostagePricingPlans = [new PostagePricingPlanResponse(postageId, "Biofreeze", 10m, 20m, 30m, 2026)]
        };
        var controller = CreateController(postagePricingPlanApiClient: apiClient, lookupApiClient: lookupApiClient);
        controller.ModelState.AddModelError("UKPrice", "A UK Price is required and must not be negative");
        var model = new PostagePricingPlanEditViewModel { PostageId = postageId, YearId = 2026, UKPrice = -1m, EUPrice = 20m, NonEUPrice = 30m };

        var result = await controller.PostagePricingPlanSave(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<PostagePricingPlanViewModel>(view.Model);
        var row = Assert.Single(resultModel.Rows);
        Assert.True(row.IsEditing);
        Assert.Equal(-1m, row.UKPrice);
        Assert.Null(apiClient.LastSetPriceRequest);
    }

    [Fact]
    public async Task PostagePricingPlanSave_ApiSaveFails_ReturnsViewWithError()
    {
        var postageId = Guid.NewGuid();
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2026, "2026/27")], false, null, null),
            SaveResult = new PostagePricingPlanSaveResult(false, new Dictionary<string, string[]> { ["UKPrice"] = ["A UK Price must not be negative"] })
        };
        var lookupApiClient = new FakeLookupApiClient
        {
            Years = [new YearResponse(2026, "2026/27")],
            PostagePricingPlans = [new PostagePricingPlanResponse(postageId, "Biofreeze", 10m, 20m, 30m, 2026)]
        };
        var controller = CreateController(postagePricingPlanApiClient: apiClient, lookupApiClient: lookupApiClient);
        var model = new PostagePricingPlanEditViewModel { PostageId = postageId, YearId = 2026, UKPrice = -1m, EUPrice = 20m, NonEUPrice = 30m };

        var result = await controller.PostagePricingPlanSave(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.IsType<PostagePricingPlanViewModel>(view.Model);
    }

    // The legacy screen displays "Dry Ice" even though the stored fldName value has no space -
    // DisplayName must cosmetically rename it without touching the underlying Name.
    [Fact]
    public async Task PostagePricingPlan_RowNamedDryice_DisplayNameIsCosmeticallyRenamed()
    {
        var postageId = Guid.NewGuid();
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2026, "2026/27")], false, null, null)
        };
        var lookupApiClient = new FakeLookupApiClient
        {
            Years = [new YearResponse(2026, "2026/27")],
            PostagePricingPlans = [new PostagePricingPlanResponse(postageId, "Dryice", 5m, 10m, 15m, 2026)]
        };
        var controller = CreateController(postagePricingPlanApiClient: apiClient, lookupApiClient: lookupApiClient);

        var result = await controller.PostagePricingPlan(2026, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PostagePricingPlanViewModel>(view.Model);
        var row = Assert.Single(model.Rows);
        Assert.Equal("Dryice", row.Name);
        Assert.Equal("Dry Ice", row.DisplayName);
    }

    [Fact]
    public async Task PostagePricingPlanRenew_Blocked_RedisplaysWithErrorMessageAndSameYear()
    {
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2025, "2025/26")], false, null, null),
            RenewResponse = new PostagePricingPlanRenewResponse(false, "No postage pricing plan has been entered for the current financial year, so the plan cannot be renewed.")
        };
        var controller = CreateController(postagePricingPlanApiClient: apiClient);

        var result = await controller.PostagePricingPlanRenew(2025, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PostagePricingPlanViewModel>(view.Model);
        Assert.True(model.MessageIsError);
        Assert.Equal(2025, model.SelectedYearId);
    }

    [Fact]
    public async Task PostagePricingPlanRenew_Succeeds_RedisplaysWithSuccessMessage()
    {
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2025, "2025/26")], false, null, null),
            RenewResponse = new PostagePricingPlanRenewResponse(true, "The postage pricing plan has successfully been renewed for the financial year 2026/27.")
        };
        var controller = CreateController(postagePricingPlanApiClient: apiClient);

        var result = await controller.PostagePricingPlanRenew(2025, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PostagePricingPlanViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Contains("2026/27", model.Message);
        Assert.Equal(1, apiClient.RenewCallCount);
    }
}

