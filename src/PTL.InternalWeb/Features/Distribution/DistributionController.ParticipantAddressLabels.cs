using Microsoft.AspNetCore.Mvc;
using PTL.InternalWeb.Features.Search;

namespace PTL.InternalWeb.Features.Distribution;

// Participant Address Labels - legacy Distributions/ParticipantAddress.aspx. Search by Lab Code,
// Lab Name or Contact Name reuses the exact same dbo.spgSearchParticipant-backed search already
// built for Contracts Admin/Search.aspx's Participants tab (Features/Search/SearchController) -
// same stored procedure, same optional/blank-means-all-records criteria, same result ordering.
public sealed partial class DistributionController
{
    // GET so a result set survives following a Print link and coming back (legacy's ViewState
    // achieves the same retention across that page's own postbacks). "searched" is absent on the
    // first visit (legacy's results panel starts hidden) and present once the Search button has
    // been used at least once - including with every field left blank, which legacy (and
    // dbo.spgSearchParticipant) treats as "match every participant", not as "no search".
    [HttpGet]
    public async Task<IActionResult> ParticipantAddressLabels(bool searched, ParticipantSearchForm? search, CancellationToken cancellationToken)
    {
        search ??= new ParticipantSearchForm();

        var results = searched && ModelState.IsValid
            ? await searchApiClient.SearchParticipantsAsync(search.ToCriteria(), cancellationToken)
            : null;

        return View(new ParticipantAddressLabelsViewModel
        {
            Search = search,
            Results = results,
        });
    }

    // [PRINT FUNCTIONALITY DEFERRED] - legacy prints directly to a Windows printer configured in
    // web.config via System.Drawing.Printing.PrintDocument (PtaBusinessObjects.Labels.LabelBase/
    // LabelParticipantAddress, dbo.spgLabelParticipantAddress). The target printing architecture
    // for this platform (thermal SDK/PrintNode/browser print/etc.) has not yet been agreed, so this
    // action only reaches the shared "not yet migrated" stub - same pattern already used for
    // Contract/Export and Distribution/ContractItems.
    [HttpGet]
    public IActionResult PrintParticipantAddressLabel(Guid participantId) =>
        View("FeatureNotAvailable", "Print Participant Address Label");
}
