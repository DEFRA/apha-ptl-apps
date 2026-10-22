using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Search;
using PTL.Core.Search;

namespace PTL.Api.Controllers;

// Legacy Contracts Admin/Search.aspx (Customers/Participants tabs) and Distributions/Search.aspx
// (Distribution Number/Sample Number tabs). Results are returned unpaged and in the stored
// procedures' own order, as the legacy grids showed them.
[ApiController]
[Route("api/search")]
public sealed class SearchController(ISearchService searchService) : ControllerBase
{
    // GET /api/search/customers?qalNumber=&accountNumber=&name=&contactName=&organisation=&countryId=
    [HttpGet("customers")]
    public async Task<ActionResult<IReadOnlyList<CustomerSearchResultResponse>>> SearchCustomers(
        [FromQuery] CustomerSearchCriteriaRequest criteria, CancellationToken cancellationToken)
    {
        var rows = await searchService.SearchCustomersAsync(criteria, cancellationToken);
        return Ok(rows.Select(r => new CustomerSearchResultResponse(
            r.CustomerId,
            r.QalNumber ?? string.Empty,
            r.AccountNumber ?? string.Empty,
            r.Name ?? string.Empty,
            r.ContactName ?? string.Empty,
            r.Organisation ?? string.Empty,
            r.Country ?? string.Empty)).ToList());
    }

    // GET /api/search/participants?labCode=&labName=&contactName=
    [HttpGet("participants")]
    public async Task<ActionResult<IReadOnlyList<ParticipantSearchResultResponse>>> SearchParticipants(
        [FromQuery] ParticipantSearchCriteriaRequest criteria, CancellationToken cancellationToken)
    {
        var rows = await searchService.SearchParticipantsAsync(criteria, cancellationToken);
        return Ok(rows.Select(r => new ParticipantSearchResultResponse(
            r.ParticipantId,
            r.CustomerId,
            r.LabCode ?? string.Empty,
            r.OldLabCode ?? string.Empty,
            r.LabName ?? string.Empty,
            r.ContactName ?? string.Empty)).ToList());
    }

    // GET /api/search/distributions?distributionNumber= - legacy Distributions/Search.aspx,
    // Distribution Number tab. An empty/omitted value returns every distribution, as in legacy.
    [HttpGet("distributions")]
    public async Task<ActionResult<IReadOnlyList<DistributionNumberSearchResultResponse>>> SearchDistributions(
        [FromQuery] DistributionNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken)
    {
        var rows = await searchService.SearchByDistributionNumberAsync(criteria, cancellationToken);
        return Ok(rows.Select(r => new DistributionNumberSearchResultResponse(
            r.MonthlyDistributionSchemeId,
            r.SchemeId,
            r.DistributionNumber ?? string.Empty,
            r.SchemeName ?? string.Empty,
            r.SchemeIdentifier ?? string.Empty,
            r.Year ?? string.Empty,
            MonthName(r.MonthId))).ToList());
    }

    // GET /api/search/samples?sampleNumber= - legacy Distributions/Search.aspx, Sample Number tab.
    // Sample Number mandatory-field validation is enforced by the caller (InternalWeb); this
    // endpoint, like the legacy stored procedure, does not itself require a non-empty value.
    [HttpGet("samples")]
    public async Task<ActionResult<IReadOnlyList<SampleNumberSearchResultResponse>>> SearchSamples(
        [FromQuery] SampleNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken)
    {
        var rows = await searchService.SearchBySampleNumberAsync(criteria, cancellationToken);
        return Ok(rows.Select(r => new SampleNumberSearchResultResponse(
            r.SampleId,
            r.SchemeId,
            r.SampleNumber ?? string.Empty,
            r.SchemeName ?? string.Empty,
            r.SchemeIdentifier ?? string.Empty,
            r.DistributionNumber ?? string.Empty,
            r.Year ?? string.Empty,
            MonthName(r.MonthId))).ToList());
    }

    // Legacy SearchSchemeByDistribution/SearchSchemeBySampleNumber.MonthId applied VB's MonthName()
    // to the raw fldMonthId column; reproduces that with the same en-GB month names used elsewhere
    // in the Distribution domain (see PackagingOutputService).
    private static string MonthName(int monthId) =>
        monthId is >= 1 and <= 12
            ? CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.GetMonthName(monthId)
            : string.Empty;
}
