using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Timeout;
using PTL.Common.Graph;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;
using PTL.Contracts.TestConsultant;
using PTL.Contracts.User;
using PTL.Contracts.WeightedPricingPlan;
using PTL.Core.User;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;
using ExternalSiteMessageResponse = PTL.Contracts.ExternalSiteMessage.ExternalSiteMessageResponse;
using ExternalSiteMessageSaveResult = PTL.Contracts.ExternalSiteMessage.ExternalSiteMessageSaveResult;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

public class SystemAdministrationControllerTests
{
    private static SystemAdministrationController CreateController(
        FakeAdministrationChargeApiClient? administrationChargeApiClient = null,
        FakeWeightedPricingPlanApiClient? weightedPricingPlanApiClient = null,
        FakePostagePricingPlanApiClient? postagePricingPlanApiClient = null,
        FakeCountryApiClient? countryApiClient = null,
        FakeExternalSiteMessageApiClient? externalSiteMessageApiClient = null,
        FakeUserApiClient? userApiClient = null,
        FakeRoleApiClient? roleApiClient = null,
        FakeLookupApiClient? lookupApiClient = null,
        FakeExternalTestConsultantApiClient? externalTestConsultantApiClient = null,
        FakeViewerApiClient? viewerApiClient = null,
        FakeStaffDirectoryService? staffDirectoryService = null) =>
        new(
            new SystemAdministrationApiClients
            {
                AdministrationCharge = administrationChargeApiClient ?? new FakeAdministrationChargeApiClient(),
                WeightedPricingPlan = weightedPricingPlanApiClient ?? new FakeWeightedPricingPlanApiClient(),
                PostagePricingPlan = postagePricingPlanApiClient ?? new FakePostagePricingPlanApiClient(),
                Country = countryApiClient ?? new FakeCountryApiClient(),
                ExternalSiteMessage = externalSiteMessageApiClient ?? new FakeExternalSiteMessageApiClient(),
                User = userApiClient ?? new FakeUserApiClient(),
                Role = roleApiClient ?? new FakeRoleApiClient(),
                Lookup = lookupApiClient ?? new FakeLookupApiClient(),
                ExternalTestConsultant = externalTestConsultantApiClient ?? new FakeExternalTestConsultantApiClient(),
                Viewer = viewerApiClient ?? new FakeViewerApiClient()
            },
            staffDirectoryService ?? new FakeStaffDirectoryService(),
            NullLogger<SystemAdministrationController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    [Fact]
    public void Index_ReturnsView()
    {
        var controller = CreateController();

        var result = controller.Index();

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
            Currencies = [new CurrencyResponse(gbpId, "British Pound", "Â£", "Â£ - British Pound"), new CurrencyResponse(eurId, "Euro", "â‚¬", "â‚¬ - Euro")]
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
                    Prices = [new AdministrationChargePriceCellViewModel { CurrencyId = currencyId, CurrencyLabel = "Â£ - British Pound", Price = 30.00m }]
                }
            ]
        };

        var result = await controller.AdministrationCharge(model, CancellationToken.None);

        Assert.Equal(nameof(SystemAdministrationController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        var request = Assert.Single(apiClient.SetPriceRequests);
        Assert.Equal(chargeId, request.AdministrationChargeId);
        Assert.Equal(30.00m, request.Price);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Administration charges saved successfully.", notification.Message);
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
                    Prices = [new AdministrationChargePriceCellViewModel { CurrencyId = Guid.NewGuid(), CurrencyLabel = "Â£ - British Pound", Price = -1.00m }]
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
                    Prices = [new AdministrationChargePriceCellViewModel { CurrencyId = Guid.NewGuid(), CurrencyLabel = "Â£ - British Pound", Price = 30.00m }]
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

        var result = await controller.Renew(2025, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeightedPricingPlanViewModel>(view.Model);
        Assert.True(model.MessageIsError);
        Assert.Equal(2025, model.SelectedYearId);
    }

    [Fact]
    public async Task Renew_NotConfirmed_ShowsTheConfirmationPageForTheNextYearWithoutRenewing()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse([new YearResponse(2025, "2025/26")], true, 2026, "2026/27")
        };
        var controller = CreateController(weightedPricingPlanApiClient: apiClient);

        var result = await controller.Renew(2025, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        var page = Assert.IsType<ConfirmationPageViewModel>(view.Model);
        Assert.Equal("Renew pricing plan for financial year 2026/27", page.Title);
        Assert.Equal("Weighted Pricing Plan", page.Caption);
        Assert.Contains("weighted pricing plan for the financial year 2026/27", page.Message, StringComparison.Ordinal);
        Assert.NotNull(page.WarningText);
        Assert.Equal("Renew pricing plan", page.ConfirmText);
        Assert.Null(page.CancelUrl);
        Assert.Equal(0, apiClient.RenewCallCount);
    }

    [Fact]
    public async Task Renew_Cancelled_ReturnsToThePricingPlanForTheSameYearWithoutRenewing()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse([new YearResponse(2025, "2025/26")], true, 2026, "2026/27")
        };
        var controller = CreateController(weightedPricingPlanApiClient: apiClient);

        var result = await controller.Renew(2025, ConfirmationFlow.Cancelled, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.WeightedPricingPlan), redirect.ActionName);
        Assert.Equal(2025, redirect.RouteValues!["yearId"]);
        Assert.Equal(0, apiClient.RenewCallCount);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Renew_Confirmed_RenewsThenRedirectsToTheSystemAdministrationIndexWithASuccessBanner()
    {
        var apiClient = new FakeWeightedPricingPlanApiClient
        {
            Years = new WeightedPricingPlanYearsResponse([new YearResponse(2025, "2025/26")], true, 2026, "2026/27"),
            RenewResponse = new WeightedPricingPlanRenewResponse(true, "The weighted pricing plan has successfully been renewed for the financial year 2026/27.")
        };
        var controller = CreateController(weightedPricingPlanApiClient: apiClient);

        var result = await controller.Renew(2025, ConfirmationFlow.Confirmed, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Contains("2026/27", notification.Message, StringComparison.Ordinal);
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
    public async Task PostagePricingPlanSave_ValidModel_SavesAndRedirectsToTheSystemAdministrationIndexWithASuccessBanner()
    {
        var postageId = Guid.NewGuid();
        var apiClient = new FakePostagePricingPlanApiClient();
        var controller = CreateController(postagePricingPlanApiClient: apiClient);
        var model = new PostagePricingPlanEditViewModel { PostageId = postageId, YearId = 2026, UKPrice = 5m, EUPrice = 10m, NonEUPrice = 15m };

        var result = await controller.PostagePricingPlanSave(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        Assert.Equal(postageId, apiClient.LastSetPriceRequest?.PostageId);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Postage pricing plan saved successfully.", notification.Message);
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

        var result = await controller.PostagePricingPlanRenew(2025, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PostagePricingPlanViewModel>(view.Model);
        Assert.True(model.MessageIsError);
        Assert.Equal(2025, model.SelectedYearId);
    }

    [Fact]
    public async Task PostagePricingPlanRenew_NotConfirmed_ShowsTheConfirmationPageForTheNextYearWithoutRenewing()
    {
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2025, "2025/26")], true, 2026, "2026/27")
        };
        var controller = CreateController(postagePricingPlanApiClient: apiClient);

        var result = await controller.PostagePricingPlanRenew(2025, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        var page = Assert.IsType<ConfirmationPageViewModel>(view.Model);
        Assert.Equal("Renew pricing plan for financial year 2026/27", page.Title);
        Assert.Equal("Postage Pricing Plan", page.Caption);
        Assert.Contains("postage pricing plan for the financial year 2026/27", page.Message, StringComparison.Ordinal);
        Assert.NotNull(page.WarningText);
        Assert.Equal("Renew pricing plan", page.ConfirmText);
        Assert.Null(page.CancelUrl);
        Assert.Equal(0, apiClient.RenewCallCount);
    }

    [Fact]
    public async Task PostagePricingPlanRenew_Cancelled_ReturnsToThePricingPlanForTheSameYearWithoutRenewing()
    {
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2025, "2025/26")], true, 2026, "2026/27")
        };
        var controller = CreateController(postagePricingPlanApiClient: apiClient);

        var result = await controller.PostagePricingPlanRenew(2025, ConfirmationFlow.Cancelled, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.PostagePricingPlan), redirect.ActionName);
        Assert.Equal(2025, redirect.RouteValues!["yearId"]);
        Assert.Equal(0, apiClient.RenewCallCount);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task PostagePricingPlanRenew_Confirmed_RenewsThenRedirectsToTheSystemAdministrationIndexWithASuccessBanner()
    {
        var apiClient = new FakePostagePricingPlanApiClient
        {
            Years = new PostagePricingPlanYearsResponse([new YearResponse(2025, "2025/26")], true, 2026, "2026/27"),
            RenewResponse = new PostagePricingPlanRenewResponse(true, "The postage pricing plan has successfully been renewed for the financial year 2026/27.")
        };
        var controller = CreateController(postagePricingPlanApiClient: apiClient);

        var result = await controller.PostagePricingPlanRenew(2025, ConfirmationFlow.Confirmed, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Contains("2026/27", notification.Message, StringComparison.Ordinal);
        Assert.Equal(1, apiClient.RenewCallCount);
    }

    [Fact]
    public async Task ExternalSiteManagement_Get_BuildsViewModelFromApiResponse()
    {
        var apiClient = new FakeExternalSiteMessageApiClient
        {
            Message = new ExternalSiteMessageResponse("<p>Body</p>", "<p>Notice</p>", "vetqas@apha.gov.uk")
        };
        var controller = CreateController(externalSiteMessageApiClient: apiClient);

        var result = await controller.ExternalSiteManagement(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ExternalSiteManagementViewModel>(view.Model);
        Assert.Equal("<p>Body</p>", model.Message);
        Assert.Equal("vetqas@apha.gov.uk", model.SupportEmailAddress);
    }

    [Fact]
    public async Task ExternalSiteManagement_Post_ValidModel_SavesAndRedirects()
    {
        var apiClient = new FakeExternalSiteMessageApiClient();
        var controller = CreateController(externalSiteMessageApiClient: apiClient);
        var model = new ExternalSiteManagementViewModel { Message = "Body", ImportantMessage = "Notice", SupportEmailAddress = "vetqas@apha.gov.uk" };

        var result = await controller.ExternalSiteManagement(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.Index), redirect.ActionName);
        Assert.Equal("vetqas@apha.gov.uk", apiClient.LastSaveRequest?.SupportEmailAddress);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("External site management saved successfully.", notification.Message);
    }

    [Fact]
    public async Task ExternalSiteManagement_Post_InvalidModelState_DoesNotCallApi()
    {
        var apiClient = new FakeExternalSiteMessageApiClient();
        var controller = CreateController(externalSiteMessageApiClient: apiClient);
        controller.ModelState.AddModelError("SupportEmailAddress", "The Email Address is required");
        var model = new ExternalSiteManagementViewModel { Message = "Body", ImportantMessage = "Notice", SupportEmailAddress = string.Empty };

        var result = await controller.ExternalSiteManagement(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<ExternalSiteManagementViewModel>(view.Model);
        Assert.Null(apiClient.LastSaveRequest);
    }

    [Fact]
    public async Task ExternalSiteManagement_Post_ApiRejectsImportantMessage_ReturnsViewWithError()
    {
        var apiClient = new FakeExternalSiteMessageApiClient
        {
            SaveResult = new ExternalSiteMessageSaveResult(false, null, new Dictionary<string, string[]> { ["ImportantMessage"] = ["The Important Message cannot exceed 500 characters."] })
        };
        var controller = CreateController(externalSiteMessageApiClient: apiClient);
        var model = new ExternalSiteManagementViewModel { Message = "Body", ImportantMessage = new string('a', 600), SupportEmailAddress = "vetqas@apha.gov.uk" };

        var result = await controller.ExternalSiteManagement(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.IsType<ExternalSiteManagementViewModel>(view.Model);
    }

    [Fact]
    public void CreateUser_Get_ReturnsFreshSearchView()
    {
        var controller = CreateController();

        var result = controller.CreateUser();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CreateUserViewModel>(view.Model);
        Assert.False(model.HasSearched);
    }

    [Fact]
    public async Task CreateUserSearch_ResultsFound_PopulatesOptions()
    {
        var directory = new FakeStaffDirectoryService
        {
            Results = [new StaffDirectoryUser { Username = "m100001", Email = "jane@apha.gov.uk", FriendlyName = "Jane Smith", FirstName = "Jane", LastName = "Smith" }]
        };
        var controller = CreateController(staffDirectoryService: directory);
        var model = new CreateUserViewModel { SearchTerm = "jane" };

        var result = await controller.CreateUserSearch(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<CreateUserViewModel>(view.Model);
        Assert.True(resultModel.HasSearched);
        Assert.Single(resultModel.ResultOptions);
        Assert.Null(resultModel.Message);
        Assert.Equal("jane", directory.LastSearchTerm);
    }

    [Fact]
    public async Task CreateUserSearch_NoResults_ShowsNoValidResultsMessage()
    {
        var controller = CreateController(staffDirectoryService: new FakeStaffDirectoryService());
        var model = new CreateUserViewModel { SearchTerm = "nobody" };

        var result = await controller.CreateUserSearch(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<CreateUserViewModel>(view.Model);
        Assert.Equal("No valid search results found", resultModel.Message);
        Assert.True(resultModel.HasSearched);
        Assert.Empty(resultModel.ResultOptions);
    }

    // Legacy GetADList: a person who already has a PT-LIMS account is not offered.
    [Fact]
    public async Task CreateUserSearch_LeavesOutPeopleWhoAlreadyHaveAnAccount()
    {
        var directory = new FakeStaffDirectoryService
        {
            Results =
            [
                new StaffDirectoryUser { Username = "dt2\\jsmith", FriendlyName = "Jane Smith" },
                new StaffDirectoryUser { Username = "dt2\\jdoe", FriendlyName = "John Doe" },
            ]
        };
        var userApiClient = new FakeUserApiClient { Users = [new UserResponse(Guid.NewGuid(), "DT2\\JSMITH", "Jane Smith", "Jane", "Smith", "jane@apha.gov.uk", "Science", false, null, 0, string.Empty)] };
        var controller = CreateController(userApiClient: userApiClient, staffDirectoryService: directory);

        var result = await controller.CreateUserSearch(new CreateUserViewModel { SearchTerm = "j" }, CancellationToken.None);

        var option = Assert.Single(Assert.IsType<CreateUserViewModel>(Assert.IsType<ViewResult>(result).Model).ResultOptions);
        Assert.Equal("dt2\\jdoe", option.Value);
    }

    [Fact]
    public async Task CreateUserSearch_EveryMatchAlreadyHasAnAccount_ShowsNoValidResultsMessage()
    {
        var directory = new FakeStaffDirectoryService { Results = [new StaffDirectoryUser { Username = "m100001", FriendlyName = "Jane Smith" }] };
        var userApiClient = new FakeUserApiClient { Users = [new UserResponse(Guid.NewGuid(), "m100001", "Jane Smith", "Jane", "Smith", "jane@apha.gov.uk", "Science", false, null, 0, string.Empty)] };
        var controller = CreateController(userApiClient: userApiClient, staffDirectoryService: directory);

        var result = await controller.CreateUserSearch(new CreateUserViewModel { SearchTerm = "jane" }, CancellationToken.None);

        var resultModel = Assert.IsType<CreateUserViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("No valid search results found", resultModel.Message);
        Assert.Empty(resultModel.ResultOptions);
    }

    [Fact]
    public async Task CreateUserSearch_TruncatedResults_ShowsRefineYourSearchMessage()
    {
        var directory = new FakeStaffDirectoryService
        {
            Results = [new StaffDirectoryUser { Username = "m100001", Email = "jane@apha.gov.uk", FriendlyName = "Jane Smith", FirstName = "Jane", LastName = "Smith" }],
            IsTruncated = true
        };
        var controller = CreateController(staffDirectoryService: directory);

        var result = await controller.CreateUserSearch(new CreateUserViewModel { SearchTerm = "j" }, CancellationToken.None);

        var resultModel = Assert.IsType<CreateUserViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Single(resultModel.ResultOptions);
        Assert.Contains("narrow your search", resultModel.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserSearch_ResultOptionsPostOnlyTheUsername()
    {
        var directory = new FakeStaffDirectoryService
        {
            Results = [new StaffDirectoryUser { Username = "dt2\\jsmith", Email = "jane|x@apha.gov.uk", FriendlyName = "Jane | Smith", FirstName = "Jane", LastName = "Smith" }]
        };
        var controller = CreateController(staffDirectoryService: directory);

        var result = await controller.CreateUserSearch(new CreateUserViewModel { SearchTerm = "jane" }, CancellationToken.None);

        var option = Assert.Single(Assert.IsType<CreateUserViewModel>(Assert.IsType<ViewResult>(result).Model).ResultOptions);
        Assert.Equal("dt2\\jsmith", option.Value);
    }

    public static TheoryData<Exception> DirectoryFailures => new()
    {
        new HttpRequestException("PTL.Api unavailable"),
        new GraphException("Microsoft Graph user search failed with HTTP 503."),
        new TimeoutRejectedException("Graph timed out"),
    };

    [Theory]
    [MemberData(nameof(DirectoryFailures))]
    public async Task CreateUserSearch_DirectoryFails_ShowsProblemInErrorSummaryInsteadOfThrowing(Exception failure)
    {
        var controller = CreateController(staffDirectoryService: new FakeStaffDirectoryService { Exception = failure });

        var result = await controller.CreateUserSearch(new CreateUserViewModel { SearchTerm = "jane" }, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<CreateUserViewModel>(view.Model);
        Assert.False(resultModel.HasSearched);
        var error = Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Contains("problem searching the staff directory", error.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserSearch_RequestTimesOut_ShowsProblemInErrorSummaryInsteadOfThrowing()
    {
        var controller = CreateController(staffDirectoryService: new FakeStaffDirectoryService { Exception = new TaskCanceledException("timeout") });

        var result = await controller.CreateUserSearch(new CreateUserViewModel { SearchTerm = "jane" }, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task CreateUserSearch_MissingSearchTerm_ReturnsViewWithoutSearching()
    {
        var directory = new FakeStaffDirectoryService();
        var controller = CreateController(staffDirectoryService: directory);
        controller.ModelState.AddModelError("SearchTerm", "Enter an Employee Number, Forename or Surname");
        var model = new CreateUserViewModel { SearchTerm = string.Empty };

        var result = await controller.CreateUserSearch(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Null(directory.LastSearchTerm);
    }

    [Fact]
    public async Task CreateUserConfirm_ValidSelection_CreatesAndShowsSuccess()
    {
        var directory = new FakeStaffDirectoryService
        {
            Results = [new StaffDirectoryUser { Username = "m100001", Email = "jane@apha.gov.uk", FriendlyName = "Jane Smith", FirstName = "Jane", LastName = "Smith" }]
        };
        var apiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: apiClient, staffDirectoryService: directory);
        var model = new CreateUserViewModel
        {
            SearchTerm = "jane",
            SelectedCandidate = "m100001",
            Department = "Science"
        };

        var result = await controller.CreateUserConfirm(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<CreateUserViewModel>(view.Model);
        Assert.True(resultModel.IsCreated);
        Assert.Equal("User added successfully", resultModel.Message);
        Assert.Equal("m100001", apiClient.LastCreateRequest?.Username);
        Assert.Equal("Science", apiClient.LastCreateRequest?.Department);
    }

    // The account details come from the directory, never from the posted form.
    [Fact]
    public async Task CreateUserConfirm_UsesTheDirectoryDetailsForTheSelectedUsername()
    {
        var directory = new FakeStaffDirectoryService
        {
            Results = [new StaffDirectoryUser { Username = "dt2\\jsmith", Email = "jane@apha.gov.uk", FriendlyName = "Jane | Smith", FirstName = "Jane", LastName = "Smith" }]
        };
        var apiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: apiClient, staffDirectoryService: directory);
        var model = new CreateUserViewModel { SearchTerm = "jane", SelectedCandidate = "DT2\\JSMITH", Department = "Science" };

        await controller.CreateUserConfirm(model, CancellationToken.None);

        var request = apiClient.LastCreateRequest;
        Assert.NotNull(request);
        Assert.Equal("dt2\\jsmith", request.Username);
        Assert.Equal("jane@apha.gov.uk", request.Email);
        Assert.Equal("Jane | Smith", request.FriendlyName);
    }

    [Fact]
    public async Task CreateUserConfirm_UsernameNotInTheDirectoryResults_DoesNotCreateAndShowsError()
    {
        var directory = new FakeStaffDirectoryService
        {
            Results = [new StaffDirectoryUser { Username = "m100001", Email = "jane@apha.gov.uk", FriendlyName = "Jane Smith", FirstName = "Jane", LastName = "Smith" }]
        };
        var apiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: apiClient, staffDirectoryService: directory);
        var model = new CreateUserViewModel { SearchTerm = "jane", SelectedCandidate = "m999999", Department = "Science" };

        var result = await controller.CreateUserConfirm(model, CancellationToken.None);

        var resultModel = Assert.IsType<CreateUserViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(apiClient.LastCreateRequest);
        Assert.False(resultModel.IsCreated);
        Assert.Contains(controller.ModelState["SelectedCandidate"]!.Errors, e => e.ErrorMessage.Contains("no longer available", StringComparison.Ordinal));
        Assert.Single(resultModel.ResultOptions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateUserConfirm_NoUserSelected_ShowsSelectAUserError(string? selected)
    {
        var directory = new FakeStaffDirectoryService
        {
            Results = [new StaffDirectoryUser { Username = "m100001", Email = "jane@apha.gov.uk", FriendlyName = "Jane Smith", FirstName = "Jane", LastName = "Smith" }]
        };
        var apiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: apiClient, staffDirectoryService: directory);
        var model = new CreateUserViewModel { SearchTerm = "jane", SelectedCandidate = selected };

        var result = await controller.CreateUserConfirm(model, CancellationToken.None);

        var resultModel = Assert.IsType<CreateUserViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(apiClient.LastCreateRequest);
        Assert.Equal("Select a user", Assert.Single(controller.ModelState["SelectedCandidate"]!.Errors).ErrorMessage);
        Assert.True(resultModel.HasSearched);
    }

    [Fact]
    public async Task CreateUserConfirm_BlankSearchTerm_RedisplaysWithoutSearchingOrCreating()
    {
        var directory = new FakeStaffDirectoryService();
        var apiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: apiClient, staffDirectoryService: directory);
        var model = new CreateUserViewModel { SearchTerm = " ", SelectedCandidate = "m100001" };

        var result = await controller.CreateUserConfirm(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.CreateUser), view.ViewName);
        Assert.Null(apiClient.LastCreateRequest);
        Assert.Equal(0, directory.SearchCallCount);
    }

    [Fact]
    public async Task CreateUserConfirm_DirectoryFails_ShowsProblemAndDoesNotCreate()
    {
        var apiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: apiClient, staffDirectoryService: new FakeStaffDirectoryService { Exception = new GraphException("Microsoft Graph user search failed with HTTP 503.") });
        var model = new CreateUserViewModel { SearchTerm = "jane", SelectedCandidate = "m100001" };

        var result = await controller.CreateUserConfirm(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Null(apiClient.LastCreateRequest);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("problem searching the staff directory", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateUserConfirm_ApiRejectsNoEmail_RedisplaysResultsWithErrorNotLinkedToAMissingField()
    {
        var apiClient = new FakeUserApiClient
        {
            SaveResult = new CreateUserSaveResult(false, null, new Dictionary<string, string[]> { ["Email"] = ["The selected User has no Email Address stored in Active Directory and cannot be added."] })
        };
        var directory = new FakeStaffDirectoryService { Results = [new StaffDirectoryUser { Username = "m100003", Email = string.Empty, FriendlyName = "Sam Taylor", FirstName = "Sam", LastName = "Taylor" }] };
        var controller = CreateController(userApiClient: apiClient, staffDirectoryService: directory);
        var model = new CreateUserViewModel
        {
            SearchTerm = "sam",
            SelectedCandidate = "m100003",
            Department = "Science"
        };

        var result = await controller.CreateUserConfirm(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.False(controller.ModelState.ContainsKey("Email"));
        Assert.Contains(controller.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("no Email Address", StringComparison.Ordinal));
        var resultModel = Assert.IsType<CreateUserViewModel>(view.Model);
        Assert.False(resultModel.IsCreated);
        Assert.True(resultModel.HasSearched);
    }

    [Fact]
    public async Task CreateUserConfirm_DepartmentTooLong_KeepsTheDepartmentFieldError()
    {
        var apiClient = new FakeUserApiClient
        {
            SaveResult = new CreateUserSaveResult(false, null, new Dictionary<string, string[]> { ["Department"] = ["Department must be 50 characters or fewer"] })
        };
        var directory = new FakeStaffDirectoryService { Results = [new StaffDirectoryUser { Username = "m100001", Email = "jane@apha.gov.uk", FriendlyName = "Jane Smith", FirstName = "Jane", LastName = "Smith" }] };
        var controller = CreateController(userApiClient: apiClient, staffDirectoryService: directory);
        var model = new CreateUserViewModel { SearchTerm = "jane", SelectedCandidate = "m100001", Department = new string('a', 60) };

        await controller.CreateUserConfirm(model, CancellationToken.None);

        Assert.True(controller.ModelState.ContainsKey("Department"));
    }

    [Fact]
    public async Task ManageUserRoles_Get_BuildsRolesAndRows()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleApiClient = new FakeRoleApiClient { Roles = [new RoleResponse(roleId, "Admin")] };
        var userApiClient = new FakeUserApiClient { UserRoleGrid = [new UserRoleRowResponse(userId, "m100001", "Jane Smith", [roleId])] };
        var controller = CreateController(userApiClient: userApiClient, roleApiClient: roleApiClient);

        var result = await controller.ManageUserRoles(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ManageUserRolesViewModel>(view.Model);
        Assert.Single(model.Roles);
        var row = Assert.Single(model.Rows);
        Assert.Contains(roleId, row.SelectedRoleIds);
    }

    // spgaRole returns fldOrder DESC; legacy's grid inserted each role at column 2, so it listed them in the reverse order.
    [Fact]
    public async Task ManageUserRoles_Get_ListsTheRoleColumnsInTheReverseOfSpgaRoleOrderLikeLegacy()
    {
        var roleApiClient = new FakeRoleApiClient
        {
            Roles = [new RoleResponse(Guid.NewGuid(), "Override Schemes"), new RoleResponse(Guid.NewGuid(), "Support"), new RoleResponse(Guid.NewGuid(), "Admin")]
        };
        var controller = CreateController(userApiClient: new FakeUserApiClient(), roleApiClient: roleApiClient);

        var result = await controller.ManageUserRoles(CancellationToken.None);

        var model = Assert.IsType<ManageUserRolesViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(["Admin", "Support", "Override Schemes"], model.Roles.Select(r => r.Name));
    }

    [Fact]
    public async Task ManageUserRoles_Post_SavesEachRowAndShowsSuccess()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: userApiClient);
        var model = new ManageUserRolesViewModel
        {
            Rows = [new UserRoleRowViewModel { UserId = userId, FriendlyName = "Jane Smith", SelectedRoleIds = [roleId] }]
        };

        var result = await controller.ManageUserRoles(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Role assignments updated successfully.", notification.Message);
        var call = Assert.Single(userApiClient.SetRolesCalls);
        Assert.Equal(userId, call.UserId);
        Assert.Contains(roleId, call.Request.RoleIds);
    }

    [Fact]
    public async Task ManageUserRoles_Post_PassesSignedInUserIdFromClaims()
    {
        var userId = Guid.NewGuid();
        var signedInUserId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: userApiClient);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(InternalUserClaimTypes.InternalUserId, signedInUserId.ToString())], "Test"))
            }
        };
        var model = new ManageUserRolesViewModel
        {
            Rows = [new UserRoleRowViewModel { UserId = userId, FriendlyName = "Jane Smith", SelectedRoleIds = [] }]
        };

        await controller.ManageUserRoles(model, CancellationToken.None);

        var call = Assert.Single(userApiClient.SetRolesCalls);
        Assert.Equal(signedInUserId, call.Request.ActingUserId);
    }

    [Fact]
    public async Task ManageUserRoles_Post_BlockedBySelfAdminRule_ShowsErrorMessage()
    {
        var userId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient
        {
            SetRolesResult = new SetUserRolesResponse(false, "You cannot remove your own Admin access.")
        };
        var controller = CreateController(userApiClient: userApiClient);
        var model = new ManageUserRolesViewModel
        {
            Rows = [new UserRoleRowViewModel { UserId = userId, FriendlyName = "Jane Smith", SelectedRoleIds = [] }]
        };

        var result = await controller.ManageUserRoles(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Contains("You cannot remove your own Admin access.", notification.Message);
    }

    [Fact]
    public async Task RemoveUser_Get_BuildsUserOptionsWithPlaceholderFirst()
    {
        var userId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient { Users = [new UserResponse(userId, "m100001", "Jane Smith", "Jane", "Smith", "jane@apha.gov.uk", "Science")] };
        var controller = CreateController(userApiClient: userApiClient);

        var result = await controller.RemoveUser(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RemoveUserViewModel>(view.Model);
        Assert.Equal(2, model.UserOptions.Count);
        Assert.Equal(string.Empty, model.UserOptions[0].Value);
        Assert.Equal(userId.ToString(), model.UserOptions[1].Value);
    }

    [Fact]
    public async Task RemoveUser_Post_NoSelection_ShowsErrorMessage()
    {
        var userApiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: userApiClient);
        var model = new RemoveUserViewModel { SelectedUserId = null };

        var result = await controller.RemoveUser(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<RemoveUserViewModel>(view.Model);
        Assert.True(resultModel.MessageIsError);
        Assert.Equal("Select a user to remove.", resultModel.Message);
        Assert.Empty(userApiClient.RemoveCalls);
    }

    [Fact]
    public async Task RemoveUser_Post_Valid_ShowsSuccess()
    {
        var userId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: userApiClient);
        var model = new RemoveUserViewModel { SelectedUserId = userId };

        var result = await controller.RemoveUser(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<RemoveUserViewModel>(view.Model);
        Assert.True(resultModel.IsRemoved);
        Assert.Equal("User Removed Successfully", resultModel.Message);
        Assert.Contains(userId, userApiClient.RemoveCalls);
    }

    [Fact]
    public async Task RemoveUser_Post_BlockedBySelfRemovalRule_ShowsErrorMessage()
    {
        var userId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient { RemoveResult = new UserRemoveResponse(false, "You cannot remove your own account.") };
        var controller = CreateController(userApiClient: userApiClient);
        var model = new RemoveUserViewModel { SelectedUserId = userId };

        var result = await controller.RemoveUser(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<RemoveUserViewModel>(view.Model);
        Assert.True(resultModel.MessageIsError);
        Assert.Equal("You cannot remove your own account.", resultModel.Message);
    }

    [Fact]
    public async Task RemoveUser_Post_PassesSignedInUserIdFromClaims()
    {
        var userId = Guid.NewGuid();
        var signedInUserId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: userApiClient);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(InternalUserClaimTypes.InternalUserId, signedInUserId.ToString())], "Test"))
            }
        };
        var model = new RemoveUserViewModel { SelectedUserId = userId };

        await controller.RemoveUser(model, CancellationToken.None);

        Assert.Equal(signedInUserId, userApiClient.LastRemoveActingUserId);
    }

    [Fact]
    public async Task RemoveUser_Post_NoSignedInClaim_PassesNullActingUserId()
    {
        var userId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: userApiClient);
        var model = new RemoveUserViewModel { SelectedUserId = userId };

        await controller.RemoveUser(model, CancellationToken.None);

        Assert.Null(userApiClient.LastRemoveActingUserId);
    }
}
