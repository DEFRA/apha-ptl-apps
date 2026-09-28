using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Lookup;
using PTL.Contracts.WeightedPricingPlan;
using PTL.Core.WeightedPricingPlan;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as ContractController/AdministrationChargeController -
// policies will be added in a later phase.
[ApiController]
[Route("api/weighted-pricing-plan")]
public sealed class WeightedPricingPlanController(IWeightedPricingPlanService weightedPricingPlanService) : ControllerBase
{
    [HttpGet("years")]
    public async Task<ActionResult<WeightedPricingPlanYearsResponse>> GetYears(CancellationToken cancellationToken)
    {
        var years = await weightedPricingPlanService.GetYearsAsync(cancellationToken);
        return Ok(new WeightedPricingPlanYearsResponse(
            years.AvailableYears.Select(y => new YearResponse(y.YearId, y.Year)).ToList(),
            years.CanRenew,
            years.NextYearId,
            years.NextYearLabel));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PricingPercentageResponse>>> GetPercentages([FromQuery] int yearId, CancellationToken cancellationToken)
    {
        var percentages = await weightedPricingPlanService.GetPercentagesForYearAsync(yearId, cancellationToken);
        return Ok(percentages
            .Select(p => new PricingPercentageResponse(p.NumberOfDistributionsOnScheme, p.NumberOfDistributionsChosen, p.Weight))
            .ToList());
    }

    [HttpPost("renew")]
    public async Task<ActionResult<WeightedPricingPlanRenewResponse>> Renew(CancellationToken cancellationToken)
    {
        var result = await weightedPricingPlanService.RenewAsync(cancellationToken);
        return Ok(new WeightedPricingPlanRenewResponse(result.Success, result.Message));
    }
}
