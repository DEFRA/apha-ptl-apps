using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;
using PTL.Contracts.TestConsultant;
using PTL.Contracts.User;
using PTL.Contracts.Viewer;
using PTL.Contracts.WeightedPricingPlan;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Tests.TestSupport;
using CountryDeleteResponse = PTL.Contracts.Country.CountryDeleteResponse;
using CountryResponse = PTL.Contracts.Country.CountryResponse;
using CountrySaveResult = PTL.Contracts.Country.CountrySaveResult;
using CountryTypeResponse = PTL.Contracts.Country.CountryTypeResponse;
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
        FakeViewerApiClient? viewerApiClient = null) =>
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

    [Fact]
    public async Task CountryManagement_Get_BuildsRowsAndTypeOptions()
    {
        var countryId = Guid.NewGuid();
        var countryTypeId = Guid.NewGuid();
        var apiClient = new FakeCountryApiClient
        {
            Countries = [new CountryResponse(countryId, "France", countryTypeId, "EU", 2)],
            CountryTypes = [new CountryTypeResponse(countryTypeId, "EU")]
        };
        var controller = CreateController(countryApiClient: apiClient);

        var result = await controller.CountryManagement(null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CountryManagementViewModel>(view.Model);
        var row = Assert.Single(model.Rows);
        Assert.Equal("France", row.Country);
        Assert.Equal(2, row.AllocationCount);
        Assert.False(row.IsEditing);
        Assert.Single(model.CountryTypeOptions);
    }

    [Fact]
    public async Task CountryManagement_Get_WithEditId_MarksMatchingRowAsEditing()
    {
        var editingId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var apiClient = new FakeCountryApiClient
        {
            Countries =
            [
                new CountryResponse(editingId, "France", Guid.NewGuid(), "EU", 0),
                new CountryResponse(otherId, "Germany", Guid.NewGuid(), "EU", 0)
            ]
        };
        var controller = CreateController(countryApiClient: apiClient);

        var result = await controller.CountryManagement(editingId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CountryManagementViewModel>(view.Model);
        Assert.True(model.Rows.Single(r => r.CountryId == editingId).IsEditing);
        Assert.False(model.Rows.Single(r => r.CountryId == otherId).IsEditing);
    }

    [Fact]
    public async Task CountryManagementAdd_ValidModel_CreatesAndRedirects()
    {
        var apiClient = new FakeCountryApiClient();
        var controller = CreateController(countryApiClient: apiClient);
        var model = new CountryManagementAddViewModel { Country = "France", CountryTypeId = Guid.NewGuid() };

        var result = await controller.CountryManagementAdd(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.CountryManagement), redirect.ActionName);
        Assert.Equal("France", apiClient.LastCreateRequest?.Country);
    }

    [Fact]
    public async Task CountryManagementAdd_ApiRejectsDuplicateName_ReturnsViewWithPrefixedError()
    {
        var apiClient = new FakeCountryApiClient
        {
            SaveResult = new CountrySaveResult(false, null, new Dictionary<string, string[]> { ["Country"] = ["This country already exists. Please try with different name."] })
        };
        var controller = CreateController(countryApiClient: apiClient);
        var model = new CountryManagementAddViewModel { Country = "France", CountryTypeId = Guid.NewGuid() };

        var result = await controller.CountryManagementAdd(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<CountryManagementViewModel>(view.Model);
        Assert.True(controller.ModelState.ContainsKey("Add.Country"));
    }

    [Fact]
    public async Task CountryManagementSave_ValidModel_UpdatesAndRedirects()
    {
        var countryId = Guid.NewGuid();
        var apiClient = new FakeCountryApiClient();
        var controller = CreateController(countryApiClient: apiClient);
        var model = new CountryManagementEditViewModel { CountryId = countryId, Country = "French Republic", CountryTypeId = Guid.NewGuid() };

        var result = await controller.CountryManagementSave(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SystemAdministrationController.CountryManagement), redirect.ActionName);
        Assert.Equal(countryId, apiClient.LastUpdateRequest?.CountryId);
    }

    [Fact]
    public async Task CountryManagementSave_ApiSaveFails_RedisplaysRowInEditModeWithPrefixedError()
    {
        var countryId = Guid.NewGuid();
        var apiClient = new FakeCountryApiClient
        {
            Countries = [new CountryResponse(countryId, "France", Guid.NewGuid(), "EU", 0)],
            SaveResult = new CountrySaveResult(false, null, new Dictionary<string, string[]> { ["Country"] = ["This country already exists. Please try with different name."] })
        };
        var controller = CreateController(countryApiClient: apiClient);
        var model = new CountryManagementEditViewModel { CountryId = countryId, Country = "Germany", CountryTypeId = Guid.NewGuid() };

        var result = await controller.CountryManagementSave(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<CountryManagementViewModel>(view.Model);
        Assert.True(resultModel.Rows.Single(r => r.CountryId == countryId).IsEditing);
        Assert.True(controller.ModelState.ContainsKey("Edit.Country"));
    }

    [Fact]
    public async Task CountryManagementRemove_CountryInUse_RedisplaysWithBlockedMessage()
    {
        var countryId = Guid.NewGuid();
        var apiClient = new FakeCountryApiClient
        {
            Countries = [new CountryResponse(countryId, "France", Guid.NewGuid(), "EU", 8)],
            DeleteResponse = new CountryDeleteResponse(false, "This country is being used by 8 customer(s)/participant(s)/Group Addresses.")
        };
        var controller = CreateController(countryApiClient: apiClient);

        var result = await controller.CountryManagementRemove(countryId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CountryManagementViewModel>(view.Model);
        Assert.True(model.MessageIsError);
        Assert.Equal("This country is being used by 8 customer(s)/participant(s)/Group Addresses.", model.Message);
        Assert.Equal(countryId, apiClient.LastDeletedCountryId);
    }

    [Fact]
    public async Task CountryManagementRemove_NoDependencies_RedisplaysWithSuccessMessage()
    {
        var countryId = Guid.NewGuid();
        var apiClient = new FakeCountryApiClient { DeleteResponse = new CountryDeleteResponse(true, null) };
        var controller = CreateController(countryApiClient: apiClient);

        var result = await controller.CountryManagementRemove(countryId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CountryManagementViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Null(model.Message);
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
        Assert.Equal(nameof(SystemAdministrationController.ExternalSiteManagement), redirect.ActionName);
        Assert.Equal("vetqas@apha.gov.uk", apiClient.LastSaveRequest?.SupportEmailAddress);
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
        var apiClient = new FakeUserApiClient
        {
            SearchResults = [new StaffDirectoryUserResponse("m100001", "jane@apha.gov.uk", "Jane Smith", "Jane", "Smith")]
        };
        var controller = CreateController(userApiClient: apiClient);
        var model = new CreateUserViewModel { SearchTerm = "jane" };

        var result = await controller.CreateUserSearch(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<CreateUserViewModel>(view.Model);
        Assert.True(resultModel.HasSearched);
        Assert.Single(resultModel.ResultOptions);
        Assert.Null(resultModel.Message);
        Assert.Equal("jane", apiClient.LastSearchTerm);
    }

    [Fact]
    public async Task CreateUserSearch_NoResults_ShowsNoValidResultsMessage()
    {
        var apiClient = new FakeUserApiClient { SearchResults = [] };
        var controller = CreateController(userApiClient: apiClient);
        var model = new CreateUserViewModel { SearchTerm = "nobody" };

        var result = await controller.CreateUserSearch(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<CreateUserViewModel>(view.Model);
        Assert.Equal("No valid search results found", resultModel.Message);
        Assert.True(resultModel.MessageIsError);
    }

    [Fact]
    public async Task CreateUserSearch_MissingSearchTerm_ReturnsViewWithoutCallingApi()
    {
        var apiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: apiClient);
        controller.ModelState.AddModelError("SearchTerm", "Enter an Employee Number, Forename or Surname");
        var model = new CreateUserViewModel { SearchTerm = string.Empty };

        var result = await controller.CreateUserSearch(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Null(apiClient.LastSearchTerm);
    }

    [Fact]
    public async Task CreateUserConfirm_ValidSelection_CreatesAndShowsSuccess()
    {
        var apiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: apiClient);
        var model = new CreateUserViewModel
        {
            SearchTerm = "jane",
            SelectedCandidate = "m100001|jane@apha.gov.uk|Jane Smith|Jane|Smith",
            Department = "Science"
        };

        var result = await controller.CreateUserConfirm(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<CreateUserViewModel>(view.Model);
        Assert.True(resultModel.IsCreated);
        Assert.Equal("m100001", apiClient.LastCreateRequest?.Username);
        Assert.Equal("Science", apiClient.LastCreateRequest?.Department);
    }

    [Fact]
    public async Task CreateUserConfirm_ApiRejectsNoEmail_RedisplaysResultsWithError()
    {
        var apiClient = new FakeUserApiClient
        {
            SaveResult = new CreateUserSaveResult(false, null, new Dictionary<string, string[]> { ["Email"] = ["The selected User has no Email Address stored in Active Directory and cannot be added."] }),
            SearchResults = [new StaffDirectoryUserResponse("m100003", string.Empty, "Sam Taylor", "Sam", "Taylor")]
        };
        var controller = CreateController(userApiClient: apiClient);
        var model = new CreateUserViewModel
        {
            SearchTerm = "sam",
            SelectedCandidate = "m100003||Sam Taylor|Sam|Taylor",
            Department = "Science"
        };

        var result = await controller.CreateUserConfirm(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        var resultModel = Assert.IsType<CreateUserViewModel>(view.Model);
        Assert.False(resultModel.IsCreated);
        Assert.True(resultModel.HasSearched);
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

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<ManageUserRolesViewModel>(view.Model);
        Assert.False(resultModel.MessageIsError);
        Assert.Equal("Role assignments updated successfully.", resultModel.Message);
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

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<ManageUserRolesViewModel>(view.Model);
        Assert.True(resultModel.MessageIsError);
        Assert.Contains("You cannot remove your own Admin access.", resultModel.Message);
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

    [Fact]
    public async Task InternalTestConsultantDepartment_Get_BuildsRows()
    {
        var userId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient
        {
            TestConsultants = [new UserResponse(userId, "m100001", "Jane Smith", "Jane", "Smith", "jane@apha.gov.uk", "Science", false, null)]
        };
        var controller = CreateController(userApiClient: userApiClient);

        var result = await controller.InternalTestConsultantDepartment(editId: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InternalTestConsultantDepartmentViewModel>(view.Model);
        var row = Assert.Single(model.Rows);
        Assert.Equal("Science", row.Department);
        Assert.False(row.IsInactive);
        Assert.False(row.IsEditing);
    }

    [Fact]
    public async Task InternalTestConsultantDepartment_Get_WithEditId_MarksMatchingRowEditing()
    {
        var userId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient
        {
            TestConsultants = [new UserResponse(userId, "m100001", "Jane Smith", "Jane", "Smith", "jane@apha.gov.uk", "Science", false, null)]
        };
        var controller = CreateController(userApiClient: userApiClient);

        var result = await controller.InternalTestConsultantDepartment(userId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InternalTestConsultantDepartmentViewModel>(view.Model);
        Assert.True(Assert.Single(model.Rows).IsEditing);
    }

    [Fact]
    public async Task InternalTestConsultantDepartmentSave_PersistsDepartmentAndRoundTripsStatus()
    {
        var userId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: userApiClient);
        var inactiveDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var model = new InternalTestConsultantEditViewModel { UserId = userId, Department = "Virology", IsInactive = true, InactiveDate = inactiveDate };

        var result = await controller.InternalTestConsultantDepartmentSave(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var call = Assert.Single(userApiClient.UpdateTestConsultantCalls);
        Assert.Equal(userId, call.UserId);
        Assert.Equal("Virology", call.Request.Department);
        Assert.True(call.Request.IsInactive);
        Assert.Equal(inactiveDate, call.Request.InactiveDate);
    }

    [Fact]
    public async Task InternalTestConsultantDepartmentToggleStatus_FromActive_SetsInactiveAndStampsDate()
    {
        var userId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: userApiClient);

        var result = await controller.InternalTestConsultantDepartmentToggleStatus(userId, "Science", isInactive: false, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var call = Assert.Single(userApiClient.UpdateTestConsultantCalls);
        Assert.Equal("Science", call.Request.Department);
        Assert.True(call.Request.IsInactive);
        Assert.NotNull(call.Request.InactiveDate);
    }

    [Fact]
    public async Task InternalTestConsultantDepartmentToggleStatus_FromInactive_SetsActiveAndClearsDate()
    {
        var userId = Guid.NewGuid();
        var userApiClient = new FakeUserApiClient();
        var controller = CreateController(userApiClient: userApiClient);

        var result = await controller.InternalTestConsultantDepartmentToggleStatus(userId, "Science", isInactive: true, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var call = Assert.Single(userApiClient.UpdateTestConsultantCalls);
        Assert.False(call.Request.IsInactive);
        Assert.Null(call.Request.InactiveDate);
    }

    [Fact]
    public async Task ExternalTestConsultantManagement_Get_BuildsRows()
    {
        var consultantId = Guid.NewGuid();
        var apiClient = new FakeExternalTestConsultantApiClient
        {
            TestConsultants = [new ExternalTestConsultantResponse(consultantId, "Jane Smith", "Science", "jane@example.com", false, null, true)]
        };
        var controller = CreateController(externalTestConsultantApiClient: apiClient);

        var result = await controller.ExternalTestConsultantManagement(editId: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ExternalTestConsultantManagementViewModel>(view.Model);
        var row = Assert.Single(model.Rows);
        Assert.Equal("Jane Smith", row.Name);
        Assert.True(row.HasLogin);
        Assert.False(row.IsEditing);
    }

    [Fact]
    public async Task ExternalTestConsultantManagementAdd_Valid_RedirectsToList()
    {
        var apiClient = new FakeExternalTestConsultantApiClient();
        var controller = CreateController(externalTestConsultantApiClient: apiClient);
        var model = new ExternalTestConsultantAddViewModel { Name = "Jane Smith", Department = "Science", Email = "jane@example.com" };

        var result = await controller.ExternalTestConsultantManagementAdd(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Jane Smith", apiClient.LastCreateRequest?.Name);
    }

    [Fact]
    public async Task ExternalTestConsultantManagementAdd_ApiRejectsMissingEmail_RedisplaysWithError()
    {
        var apiClient = new FakeExternalTestConsultantApiClient
        {
            SaveResult = new ExternalTestConsultantSaveResult(false, null, new Dictionary<string, string[]> { ["Email"] = ["Enter an email address"] })
        };
        var controller = CreateController(externalTestConsultantApiClient: apiClient);
        var model = new ExternalTestConsultantAddViewModel { Name = "Jane Smith", Department = "Science", Email = "jane@example.com" };

        var result = await controller.ExternalTestConsultantManagementAdd(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.IsType<ExternalTestConsultantManagementViewModel>(view.Model);
    }

    [Fact]
    public async Task ExternalTestConsultantManagementSave_Valid_RedirectsToList()
    {
        var consultantId = Guid.NewGuid();
        var apiClient = new FakeExternalTestConsultantApiClient();
        var controller = CreateController(externalTestConsultantApiClient: apiClient);
        var model = new ExternalTestConsultantEditViewModel { ExternalTestConsultantId = consultantId, Name = "Jane Smith", Department = "Science", Email = "jane@example.com" };

        var result = await controller.ExternalTestConsultantManagementSave(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var call = Assert.Single(apiClient.UpdateCalls);
        Assert.Equal(consultantId, call.ExternalTestConsultantId);
    }

    [Fact]
    public async Task ExternalTestConsultantManagementToggleStatus_FromActive_TogglesToInactive()
    {
        var consultantId = Guid.NewGuid();
        var apiClient = new FakeExternalTestConsultantApiClient();
        var controller = CreateController(externalTestConsultantApiClient: apiClient);

        var result = await controller.ExternalTestConsultantManagementToggleStatus(consultantId, isInactive: false, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var call = Assert.Single(apiClient.SetStatusCalls);
        Assert.Equal(consultantId, call.ExternalTestConsultantId);
        Assert.True(call.IsInactive);
    }

    [Fact]
    public async Task ExternalTestConsultantManagementGenerateLogin_Success_ShowsSuccessMessage()
    {
        var consultantId = Guid.NewGuid();
        var apiClient = new FakeExternalTestConsultantApiClient { GenerateLoginResult = new GenerateLoginResponse(true, null) };
        var controller = CreateController(externalTestConsultantApiClient: apiClient);

        var result = await controller.ExternalTestConsultantManagementGenerateLogin(consultantId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ExternalTestConsultantManagementViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Contains(consultantId, apiClient.GenerateLoginCalls);
    }

    [Fact]
    public async Task ExternalTestConsultantManagementGenerateLogin_Failure_ShowsErrorMessage()
    {
        var consultantId = Guid.NewGuid();
        var apiClient = new FakeExternalTestConsultantApiClient { GenerateLoginResult = new GenerateLoginResponse(false, "The login could not be generated.") };
        var controller = CreateController(externalTestConsultantApiClient: apiClient);

        var result = await controller.ExternalTestConsultantManagementGenerateLogin(consultantId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ExternalTestConsultantManagementViewModel>(view.Model);
        Assert.True(model.MessageIsError);
        Assert.Equal("The login could not be generated.", model.Message);
    }

    [Fact]
    public async Task ViewerManagement_Get_BuildsRows()
    {
        var viewerId = Guid.NewGuid();
        var apiClient = new FakeViewerApiClient
        {
            Viewers = [new ViewerResponse(viewerId, "Jane Smith", "jane@example.com", true, [], [])]
        };
        var controller = CreateController(viewerApiClient: apiClient);

        var result = await controller.ViewerManagement(editId: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ViewerManagementViewModel>(view.Model);
        var row = Assert.Single(model.Rows);
        Assert.Equal("Jane Smith", row.Name);
        Assert.True(row.HasLogin);
        Assert.False(row.IsEditing);
    }

    [Fact]
    public async Task ViewerManagement_Get_WithAssignments_BuildsEnrichedRemoveConfirmMessage()
    {
        var viewerId = Guid.NewGuid();
        var apiClient = new FakeViewerApiClient
        {
            Viewers =
            [
                new ViewerResponse(viewerId, "Jane Smith", "jane@example.com", false,
                    [new ViewerSchemeResponse("SFW1234", "Heavy Metals")],
                    [new ViewerParticipantResponse("LAB001", "Example Lab")])
            ]
        };
        var controller = CreateController(viewerApiClient: apiClient);

        var result = await controller.ViewerManagement(editId: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ViewerManagementViewModel>(view.Model);
        var row = Assert.Single(model.Rows);
        Assert.Contains("SFW1234: Heavy Metals", row.RemoveConfirmMessage);
        Assert.Contains("LAB001: Example Lab", row.RemoveConfirmMessage);
    }

    [Fact]
    public async Task ViewerManagementAdd_Valid_RedirectsToList()
    {
        var apiClient = new FakeViewerApiClient();
        var controller = CreateController(viewerApiClient: apiClient);
        var model = new ViewerAddViewModel { Name = "Jane Smith", Email = "jane@example.com" };

        var result = await controller.ViewerManagementAdd(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Jane Smith", apiClient.LastCreateRequest?.Name);
    }

    [Fact]
    public async Task ViewerManagementAdd_ApiRejectsMissingEmail_RedisplaysWithError()
    {
        var apiClient = new FakeViewerApiClient
        {
            SaveResult = new ViewerSaveResult(false, null, new Dictionary<string, string[]> { ["Email"] = ["Enter an email address"] })
        };
        var controller = CreateController(viewerApiClient: apiClient);
        var model = new ViewerAddViewModel { Name = "Jane Smith", Email = "jane@example.com" };

        var result = await controller.ViewerManagementAdd(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.IsType<ViewerManagementViewModel>(view.Model);
    }

    [Fact]
    public async Task ViewerManagementSave_Valid_RedirectsToList()
    {
        var viewerId = Guid.NewGuid();
        var apiClient = new FakeViewerApiClient();
        var controller = CreateController(viewerApiClient: apiClient);
        var model = new ViewerEditViewModel { ViewerId = viewerId, Name = "Jane Smith", Email = "jane@example.com" };

        var result = await controller.ViewerManagementSave(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var call = Assert.Single(apiClient.UpdateCalls);
        Assert.Equal(viewerId, call.ViewerId);
    }

    [Fact]
    public async Task ViewerManagementRemove_Success_ShowsSuccessMessage()
    {
        var viewerId = Guid.NewGuid();
        var apiClient = new FakeViewerApiClient { DeleteResult = new ViewerDeleteResponse(true, null) };
        var controller = CreateController(viewerApiClient: apiClient);

        var result = await controller.ViewerManagementRemove(viewerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ViewerManagementViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Contains(viewerId, apiClient.DeleteCalls);
    }

    [Fact]
    public async Task ViewerManagementGenerateLogin_Success_ShowsSuccessMessage()
    {
        var viewerId = Guid.NewGuid();
        var apiClient = new FakeViewerApiClient { GenerateLoginResult = new GenerateLoginResponse(true, null) };
        var controller = CreateController(viewerApiClient: apiClient);

        var result = await controller.ViewerManagementGenerateLogin(viewerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ViewerManagementViewModel>(view.Model);
        Assert.False(model.MessageIsError);
        Assert.Contains(viewerId, apiClient.GenerateLoginCalls);
    }

    [Fact]
    public async Task ViewerManagementGenerateLogin_Failure_ShowsErrorMessage()
    {
        var viewerId = Guid.NewGuid();
        var apiClient = new FakeViewerApiClient { GenerateLoginResult = new GenerateLoginResponse(false, "The login could not be generated.") };
        var controller = CreateController(viewerApiClient: apiClient);

        var result = await controller.ViewerManagementGenerateLogin(viewerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ViewerManagementViewModel>(view.Model);
        Assert.True(model.MessageIsError);
        Assert.Equal("The login could not be generated.", model.Message);
    }
}

