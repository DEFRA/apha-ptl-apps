using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Search;
using PTL.Core.Search;

namespace PTL.Api.Controllers;

// Legacy Contracts Admin/Search.aspx - Customers and Participants tabs. Results are returned
// unpaged and in the stored procedures' own order, as the legacy grids showed them.
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
}
