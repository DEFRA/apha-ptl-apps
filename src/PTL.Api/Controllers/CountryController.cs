using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Country;
using PTL.Core.Country;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as the other System Administration controllers.
//
// Reads of the simple Country name/id pair already have a home at GET /api/lookups/countries
// (used by Customer/Participant/GroupAddress dropdowns) - this controller owns the richer
// Country Management admin screen (Country Type + AllocationCount + create/update/delete), which
// that shared lookup deliberately does not expose.
[ApiController]
[Route("api/countries")]
public sealed class CountryController(ICountryService countryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CountryResponse>>> GetCountries(CancellationToken cancellationToken)
    {
        var countries = await countryService.GetAllAsync(cancellationToken);
        return Ok(countries.Select(ToResponse).ToList());
    }

    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<CountryTypeResponse>>> GetCountryTypes(CancellationToken cancellationToken)
    {
        var countryTypes = await countryService.GetCountryTypesAsync(cancellationToken);
        return Ok(countryTypes.Select(t => new CountryTypeResponse(t.CountryTypeId, t.CountryType)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CountryResponse>> CreateCountry([FromBody] CountrySaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await countryService.CreateAsync(request.Country, request.CountryTypeId, cancellationToken);
            return Ok(ToResponse(created));
        }
        catch (CountryValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("{countryId:guid}")]
    public async Task<ActionResult<CountryResponse>> UpdateCountry(Guid countryId, [FromBody] CountrySaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await countryService.UpdateAsync(countryId, request.Country, request.CountryTypeId, cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (CountryValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    // Returns 200 OK with Success=false (not 400/404/409) when removal is blocked - this is a
    // legitimate business outcome (country still in use, or already gone), not an invalid
    // request, matching how PostagePricingPlanController.Renew reports a blocked renewal.
    [HttpDelete("{countryId:guid}")]
    public async Task<ActionResult<CountryDeleteResponse>> DeleteCountry(Guid countryId, CancellationToken cancellationToken)
    {
        var result = await countryService.DeleteAsync(countryId, cancellationToken);
        return Ok(new CountryDeleteResponse(result.Success, result.Message));
    }

    private ActionResult ToValidationProblem(CountryValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static CountryResponse ToResponse(PTL.Core.Country.Country country) =>
        new(country.CountryId, country.CountryName, country.CountryTypeId, country.CountryType, country.AllocationCount);
}
