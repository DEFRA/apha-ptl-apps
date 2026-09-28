using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Lookup;
using PTL.InternalWeb.Features.AdministrationCharge;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.AdministrationCharge;

public class AdministrationChargeControllerTests
{
    private static AdministrationChargeController CreateController(FakeAdministrationChargeApiClient apiClient, FakeLookupApiClient? lookupApiClient = null) =>
        new(apiClient, lookupApiClient ?? new FakeLookupApiClient(), NullLogger<AdministrationChargeController>.Instance);

    [Fact]
    public async Task Index_Get_BuildsOneRowPerChargeWithACellPerCurrency()
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
        var controller = CreateController(apiClient, lookupApiClient);

        var result = await controller.Index(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdministrationChargeListViewModel>(view.Model);
        var charge = Assert.Single(model.Charges);
        Assert.Equal(2, charge.Prices.Count);
        Assert.Equal(25.00m, charge.Prices.Single(p => p.CurrencyId == gbpId).Price);
        Assert.Equal(0m, charge.Prices.Single(p => p.CurrencyId == eurId).Price);
    }

    [Fact]
    public async Task Index_Post_ValidModel_SavesEveryCellAndRedirects()
    {
        var chargeId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var apiClient = new FakeAdministrationChargeApiClient();
        var controller = CreateController(apiClient);
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

        var result = await controller.Index(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var request = Assert.Single(apiClient.SetPriceRequests);
        Assert.Equal(chargeId, request.AdministrationChargeId);
        Assert.Equal(30.00m, request.Price);
    }

    [Fact]
    public async Task Index_Post_InvalidModel_ReturnsViewWithoutSaving()
    {
        var apiClient = new FakeAdministrationChargeApiClient();
        var controller = CreateController(apiClient);
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

        var result = await controller.Index(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Empty(apiClient.SetPriceRequests);
    }

    [Fact]
    public async Task Index_Post_ApiSaveFails_ReturnsViewWithError()
    {
        var apiClient = new FakeAdministrationChargeApiClient { SetPriceSucceeds = false };
        var controller = CreateController(apiClient);
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

        var result = await controller.Index(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }
}
