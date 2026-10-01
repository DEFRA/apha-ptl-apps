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

    // GET /api/lookups/years/all - every year on record, for mapping historical contract rows'
    // YearId to display text (legacy GetYearNameFromYearId on ContractList.aspx.vb).
    [HttpGet("years/all")]
    public async Task<ActionResult<IReadOnlyList<YearResponse>>> GetAllYears(CancellationToken cancellationToken)
    {
        var years = await lookupService.GetAllYearsAsync(cancellationToken);
        return Ok(years.Select(y => new YearResponse(y.YearId, y.Year)).ToList());
    }

    // GET /api/lookups/years/weighted-pricing - years that have a weighted-pricing plan configured
    // (matches legacy WeightedPricingYearCollection.PricingPlanExists, used by ParticipantScheme.
    // aspx.vb's LoadPricingOptions to decide which Pricing Plan options to offer).
    [HttpGet("years/weighted-pricing")]
    public async Task<ActionResult<IReadOnlyList<YearResponse>>> GetWeightedPricingYears(CancellationToken cancellationToken)
    {
        var years = await lookupService.GetWeightedPricingYearsAsync(cancellationToken);
        return Ok(years.Select(y => new YearResponse(y.YearId, y.Year)).ToList());
    }

    // GET /api/lookups/group-addresses - see ParticipantScheme.aspx's "Select a Group Address" popup.
    [HttpGet("group-addresses")]
    public async Task<ActionResult<IReadOnlyList<GroupAddressResponse>>> GetGroupAddresses(CancellationToken cancellationToken)
    {
        var groupAddresses = await lookupService.GetGroupAddressesAsync(cancellationToken);
        return Ok(groupAddresses.Select(g => new GroupAddressResponse(g.GroupAddressId, g.Identifier, g.Address1, g.CountryId)).ToList());
    }

    // GET /api/lookups/schemes/{schemeId}/currencies - see docs/analysis/scheme-analysis.md,
    // "Scheme Currency Read Operations".
    [HttpGet("schemes/{schemeId:guid}/currencies")]
    public async Task<ActionResult<IReadOnlyList<SchemeCurrencyResponse>>> GetSchemeCurrencies(Guid schemeId, CancellationToken cancellationToken)
    {
        var currencies = await lookupService.GetSchemeCurrenciesAsync(schemeId, cancellationToken);
        return Ok(currencies.Select(c => new SchemeCurrencyResponse(c.SchemeCurrencyId, c.SchemeId, c.CurrencyId, c.Price, c.CurrencyName, c.CurrencySymbol)).ToList());
    }

    // GET /api/lookups/postage-pricing-plans?year={yearId} - see docs/analysis/scheme-analysis.md,
    // "Postage Pricing Plan Read Operations".
    [HttpGet("postage-pricing-plans")]
    public async Task<ActionResult<IReadOnlyList<PostagePricingPlanResponse>>> GetPostagePricingPlans([FromQuery] int year, CancellationToken cancellationToken)
    {
        var plans = await lookupService.GetPostagePricingPlansForYearAsync(year, cancellationToken);
        return Ok(plans.Select(p => new PostagePricingPlanResponse(p.PostageId, p.Name, p.UKPrice, p.EUPrice, p.NonEUPrice, p.YearId)).ToList());
    }

    // GET /api/lookups/system-settings - see docs/analysis/contract-analysis.md,
    // "Default pricing derivation on creation" (UT number default for a new contract).
    [HttpGet("system-settings")]
    public async Task<ActionResult<SystemSettingsResponse>> GetSystemSettings(CancellationToken cancellationToken)
    {
        var settings = await lookupService.GetSystemSettingsAsync(cancellationToken);
        return Ok(new SystemSettingsResponse(settings.UTNumber));
    }
}
