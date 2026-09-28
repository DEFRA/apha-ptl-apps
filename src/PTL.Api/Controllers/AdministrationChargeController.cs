using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.AdministrationCharge;
using PTL.Core.AdministrationCharge;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as ContractController - policies will be added in a later phase.
[ApiController]
[Route("api/administration-charges")]
public sealed class AdministrationChargeController(IAdministrationChargeService administrationChargeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdministrationChargeResponse>>> GetAdministrationCharges(CancellationToken cancellationToken)
    {
        var charges = await administrationChargeService.GetChargesAsync(cancellationToken);
        var prices = await administrationChargeService.GetPricesAsync(cancellationToken);

        var response = charges
            .Select(charge => new AdministrationChargeResponse(
                charge.AdministrationChargeId,
                charge.Name,
                prices
                    .Where(price => price.AdministrationChargeId == charge.AdministrationChargeId)
                    .Select(price => new AdministrationChargeCurrencyPriceResponse(price.CurrencyId, price.Price))
                    .ToList()))
            .ToList();

        return Ok(response);
    }

    [HttpPut("price")]
    public async Task<ActionResult<AdministrationChargeCurrencyPriceResponse>> SetPrice([FromBody] UpdateAdministrationChargePriceRequest request, CancellationToken cancellationToken)
    {
        var result = await administrationChargeService.SetPriceAsync(request.AdministrationChargeId, request.CurrencyId, request.Price, cancellationToken);
        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            return ValidationProblem(ModelState);
        }

        return Ok(new AdministrationChargeCurrencyPriceResponse(request.CurrencyId, request.Price));
    }
}
