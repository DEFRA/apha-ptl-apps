using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;
using PTL.Core.PostagePricingPlan;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as AdministrationChargeController/WeightedPricingPlanController -
// policies will be added in a later phase.
//
// Reads of the postage pricing plan rows for a given year already have a home at
// GET /api/lookups/postage-pricing-plans (spgPostageByYearID, shared with the Scheme postage
// dropdown) - this controller only adds the years/edit/renew operations that domain doesn't own.
[ApiController]
[Route("api/postage-pricing-plan")]
public sealed class PostagePricingPlanController(IPostagePricingPlanService postagePricingPlanService) : ControllerBase
{
    [HttpGet("years")]
    public async Task<ActionResult<PostagePricingPlanYearsResponse>> GetYears(CancellationToken cancellationToken)
    {
        var years = await postagePricingPlanService.GetYearsAsync(cancellationToken);
        return Ok(new PostagePricingPlanYearsResponse(
            years.AvailableYears.Select(y => new YearResponse(y.YearId, y.Year)).ToList(),
            years.CanRenew,
            years.NextYearId,
            years.NextYearLabel));
    }

    [HttpPut("price")]
    public async Task<ActionResult<PostagePricingPlanSaveResult>> SetPrice([FromBody] UpdatePostagePricingPlanPriceRequest request, CancellationToken cancellationToken)
    {
        var result = await postagePricingPlanService.SetPriceAsync(request.PostageId, request.UKPrice, request.EUPrice, request.NonEUPrice, cancellationToken);
        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            return ValidationProblem(ModelState);
        }

        return Ok(new PostagePricingPlanSaveResult(true, new Dictionary<string, string[]>()));
    }

    [HttpPost("renew")]
    public async Task<ActionResult<PostagePricingPlanRenewResponse>> Renew(CancellationToken cancellationToken)
    {
        var result = await postagePricingPlanService.RenewAsync(cancellationToken);
        return Ok(new PostagePricingPlanRenewResponse(result.Success, result.Message));
    }
}
