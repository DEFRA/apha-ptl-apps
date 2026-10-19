using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;

namespace PTL.InternalWeb.Features.Search;

// Legacy Contracts Admin/Search.aspx - "This page can be used to search for Customers and
// Participants." Authentication/authorization are out of scope for this phase, consistent with every
// other InternalWeb controller (legacy: Manage Contracts section, ProficiencyTestingInternalUser).
public class SearchController(ISearchApiClient searchApiClient, ILookupApiClient lookupApiClient) : Controller
{
    private const string NotSpecifiedOptionText = "- Not Specified -";

    // GET so a result set survives following a Details link and coming back. tab is absent on the first
    // visit (nothing searched); each tab's Search button submits only its own criteria with its tab.
    [HttpGet]
    public async Task<IActionResult> Index(
        SearchTab? tab,
        [Bind(Prefix = nameof(SearchViewModel.Customer))] CustomerSearchForm? customer,
        [Bind(Prefix = nameof(SearchViewModel.Participant))] ParticipantSearchForm? participant,
        CancellationToken cancellationToken)
    {
        customer ??= new CustomerSearchForm();
        participant ??= new ParticipantSearchForm();

        // Each tab's form submits only its own fields, so errors here belong to the searched tab.
        var isValid = ModelState.IsValid;

        var customerResults = tab == SearchTab.Customers && isValid
            ? await searchApiClient.SearchCustomersAsync(customer.ToCriteria(), cancellationToken)
            : null;

        var participantResults = tab == SearchTab.Participants && isValid
            ? await searchApiClient.SearchParticipantsAsync(participant.ToCriteria(), cancellationToken)
            : null;

        var countries = await lookupApiClient.GetCountriesAsync(cancellationToken);

        return View(new SearchViewModel
        {
            ActiveTab = tab ?? SearchTab.Customers,
            Customer = customer,
            Participant = participant,
            CountryOptions =
            [
                new SelectListItem(NotSpecifiedOptionText, string.Empty),
                .. countries.Select(c => new SelectListItem(c.Country, c.CountryId.ToString())),
            ],
            CustomerResults = customerResults,
            ParticipantResults = participantResults,
        });
    }
}
