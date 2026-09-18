using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Lookup;
using PTL.Core.Lookup;

namespace PTL.Api.Controllers;

// Read-only reference data for populating dropdowns (Country/Currency/CustomerType) on the
// Customer Create/Edit screens - see docs/analysis/customer-analysis.md. No create/update/delete:
// these lists are maintained elsewhere (not exposed anywhere in the legacy Web Forms UI either).
[ApiController]
[Route("api/lookups")]
public sealed class LookupController(ILookupService lookupService) : ControllerBase
{
    [HttpGet("countries")]
    public async Task<ActionResult<IReadOnlyList<CountryResponse>>> GetCountries(CancellationToken cancellationToken)
    {
        var countries = await lookupService.GetCountriesAsync(cancellationToken);
        return Ok(countries.Select(c => new CountryResponse(c.CountryId, c.Country)).ToList());
    }

    [HttpGet("currencies")]
    public async Task<ActionResult<IReadOnlyList<CurrencyResponse>>> GetCurrencies(CancellationToken cancellationToken)
    {
        var currencies = await lookupService.GetCurrenciesAsync(cancellationToken);
        return Ok(currencies.Select(c => new CurrencyResponse(c.CurrencyId, c.Name, c.Symbol, c.LongName)).ToList());
    }

    [HttpGet("customer-types")]
    public async Task<ActionResult<IReadOnlyList<CustomerTypeResponse>>> GetCustomerTypes(CancellationToken cancellationToken)
    {
        var customerTypes = await lookupService.GetCustomerTypesAsync(cancellationToken);
        return Ok(customerTypes.Select(c => new CustomerTypeResponse(c.CustomerTypeId, c.CustomerType)).ToList());
    }

    [HttpGet("vat-ratings")]
    public async Task<ActionResult<IReadOnlyList<VatRatingResponse>>> GetVatRatings(CancellationToken cancellationToken)
    {
        var vatRatings = await lookupService.GetVatRatingsAsync(cancellationToken);
        return Ok(vatRatings.Select(v => new VatRatingResponse(v.VatRatingId, v.VatRating)).ToList());
    }

    [HttpGet("lab-types")]
    public async Task<ActionResult<IReadOnlyList<LabTypeResponse>>> GetLabTypes(CancellationToken cancellationToken)
    {
        var labTypes = await lookupService.GetLabTypesAsync(cancellationToken);
        return Ok(labTypes.Select(l => new LabTypeResponse(l.LabTypeId, l.Name)).ToList());
    }

    [HttpGet("years")]
    public async Task<ActionResult<IReadOnlyList<YearResponse>>> GetCurrentYears(CancellationToken cancellationToken)
    {
        var years = await lookupService.GetCurrentYearsAsync(cancellationToken);
        return Ok(years.Select(y => new YearResponse(y.YearId, y.Year)).ToList());
    }
}
