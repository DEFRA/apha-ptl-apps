using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.AdministrationCharge;
using PTL.Contracts.AdministrationCharge;
using PTL.Core.AdministrationCharge;

namespace PTL.Api.Tests.Endpoints;

public class AdministrationChargeControllerTests
{
    private static AdministrationChargeController CreateController(FakeAdministrationChargeRepository repository)
    {
        var controller = new AdministrationChargeController(new AdministrationChargeService(repository));

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
    public async Task GetAdministrationCharges_GroupsPricesUnderTheirCharge()
    {
        var chargeId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var repository = new FakeAdministrationChargeRepository
        {
            Charges = [new AdministrationChargeEntity { AdministrationChargeId = chargeId, Name = "Administration Charge" }],
            Prices = [new AdministrationChargeCurrencyEntity { AdministrationChargeCurrencyId = Guid.NewGuid(), AdministrationChargeId = chargeId, CurrencyId = currencyId, Price = 25.00m }]
        };
        var controller = CreateController(repository);

        var result = await controller.GetAdministrationCharges(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsAssignableFrom<IReadOnlyList<AdministrationChargeResponse>>(ok.Value);
        var charge = Assert.Single(response);
        var price = Assert.Single(charge.Prices);
        Assert.Equal(currencyId, price.CurrencyId);
        Assert.Equal(25.00m, price.Price);
    }

    [Fact]
    public async Task SetPrice_ValidRequest_ReturnsOkWithPersistedPrice()
    {
        var controller = CreateController(new FakeAdministrationChargeRepository());
        var request = new UpdateAdministrationChargePriceRequest(Guid.NewGuid(), Guid.NewGuid(), 30.00m);

        var result = await controller.SetPrice(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var price = Assert.IsType<AdministrationChargeCurrencyPriceResponse>(ok.Value);
        Assert.Equal(30.00m, price.Price);
    }

    [Fact]
    public async Task SetPrice_NegativePrice_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeAdministrationChargeRepository());
        var request = new UpdateAdministrationChargePriceRequest(Guid.NewGuid(), Guid.NewGuid(), -1.00m);

        var result = await controller.SetPrice(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
