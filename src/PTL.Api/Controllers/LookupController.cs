using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Lookup;
using PTL.Contracts.Participant;
using PTL.Contracts.Scheme;
using PTL.Core.Lookup;
using PTL.Core.Viewer;

namespace PTL.Api.Controllers;

// Read-only reference data for populating dropdowns (Country/Currency/CustomerType) on the
// Customer Create/Edit screens - see docs/analysis/customer-analysis.md. No create/update/delete:
// these lists are maintained elsewhere (not exposed anywhere in the legacy Web Forms UI either).
[ApiController]
[Route("api/lookups")]
public sealed class LookupController(ILookupService lookupService, IViewerRepository viewerRepository) : ControllerBase
{
    // GET /api/lookups/viewers - tblViewer master list, used by the Scheme Viewers tab.
    [HttpGet("viewers")]
    public async Task<ActionResult<IReadOnlyList<ViewerResponse>>> GetViewers(CancellationToken cancellationToken)
    {
        var viewers = await viewerRepository.GetAllAsync(cancellationToken);
        return Ok(viewers.Select(v => new ViewerResponse(v.ViewerId, v.Name, v.Email)).ToList());
    }
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

    // GET /api/lookups/schedules | /schedule-codes | /days - the three Scheme "Details" tab
    // dropdowns (legacy DropDownSchedule / DropDownScheduleCode / DropDownDayOfWeek).
    [HttpGet("schedules")]
    public async Task<ActionResult<IReadOnlyList<ScheduleResponse>>> GetSchedules(CancellationToken cancellationToken)
    {
        var schedules = await lookupService.GetSchedulesAsync(cancellationToken);
        return Ok(schedules.Select(s => new ScheduleResponse(s.ScheduleId, s.Schedule)).ToList());
    }

    [HttpGet("schedule-codes")]
    public async Task<ActionResult<IReadOnlyList<ScheduleCodeResponse>>> GetScheduleCodes(CancellationToken cancellationToken)
    {
        var scheduleCodes = await lookupService.GetScheduleCodesAsync(cancellationToken);
        return Ok(scheduleCodes.Select(s => new ScheduleCodeResponse(s.ScheduleCodeId, s.ScheduleCode)).ToList());
    }

    [HttpGet("days")]
    public async Task<ActionResult<IReadOnlyList<DayResponse>>> GetDays(CancellationToken cancellationToken)
    {
        var days = await lookupService.GetDaysAsync(cancellationToken);
        return Ok(days.Select(d => new DayResponse(d.DayId, d.Day)).ToList());
    }

    // GET /api/lookups/scheme-month-editability?year={yearId} - which distribution months a scheme
    // in that year may still change (legacy Scheme.SetEditPermissions). The equivalent flags for an
    // existing scheme already arrive on SchemeResponse.CanEdit; this serves the Create screen,
    // which has no scheme to read them from yet.
    [HttpGet("scheme-month-editability")]
    public async Task<ActionResult<SchemeMonthEditabilityResponse>> GetSchemeMonthEditability([FromQuery] int year, CancellationToken cancellationToken)
    {
        var editability = await lookupService.GetSchemeMonthEditabilityAsync(year, cancellationToken);
        return Ok(new SchemeMonthEditabilityResponse(
            editability.Jan, editability.Feb, editability.Mar, editability.Apr,
            editability.May, editability.Jun, editability.Jul, editability.Aug,
            editability.Sep, editability.Oct, editability.Nov, editability.Dec));
    }

    // GET /api/lookups/test-consultants | /assessors - the Scheme screen's Test Consultants and
    // Assessors tabs. Inactive people are already filtered out by LookupService.
    [HttpGet("test-consultants")]
    public async Task<ActionResult<IReadOnlyList<SchemeUserResponse>>> GetTestConsultants(CancellationToken cancellationToken)
    {
        var consultants = await lookupService.GetTestConsultantsAsync(cancellationToken);
        return Ok(consultants.Select(c => new SchemeUserResponse(c.UserId, c.FriendlyName, c.IsExternal)).ToList());
    }

    [HttpGet("assessors")]
    public async Task<ActionResult<IReadOnlyList<SchemeUserResponse>>> GetAssessors(CancellationToken cancellationToken)
    {
        var assessors = await lookupService.GetAssessorsAsync(cancellationToken);
        return Ok(assessors.Select(a => new SchemeUserResponse(a.UserId, a.FriendlyName, a.IsExternal)).ToList());
    }

    // GET /api/lookups/scheme-item-types?kind={kind}&year={yearId} - the Tests tab "Add"
    // dropdowns, scoped to the scheme's year.
    [HttpGet("scheme-item-types")]
    public async Task<ActionResult<IReadOnlyList<SchemeItemTypeResponse>>> GetSchemeItemTypes([FromQuery] SchemeItemTypeKind kind, [FromQuery] int year, CancellationToken cancellationToken)
    {
        var itemTypes = await lookupService.GetSchemeItemTypesAsync(kind, year, cancellationToken);
        return Ok(itemTypes.Select(t => new SchemeItemTypeResponse(t.ItemTypeId, t.Name, t.NoLongerInUse)).ToList());
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
        return Ok(new SystemSettingsResponse(settings.UTNumber, settings.ContractStartDate, settings.CurrentYearId, settings.NextYearId));
    }
}
