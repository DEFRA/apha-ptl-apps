using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PTL.Core.Labels;
using PTL.InternalWeb.Features.Search;
using PTL.SharedUI.Notifications;

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

    // Legacy ParticipantAddress.aspx LinkButtonPrint_Click: a fresh ParticipantAddressInfo fetch by
    // id (not the search result row already on screen), composed via LabelParticipantAddress and
    // sent straight to a Windows printer (PtaBusinessObjects.Labels.LabelBase, web.config
    // LabelPrinterName). Printing is now delegated to PTL.Api (ILabelPrinterApiClient), which holds
    // the actual printer connection - the same participantApiClient fetch as
    // Features/Participant/ParticipantController's own "Print Address Label" action. The search
    // criteria are round-tripped through the Print link so the redirect back restores the same
    // result list legacy's ViewState would have kept on screen.
    [HttpGet]
    public async Task<IActionResult> PrintParticipantAddressLabel(
        Guid participantId, string? labCode, string? labName, string? contactName, CancellationToken cancellationToken)
    {
        var participant = await participantApiClient.GetParticipantAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return NotFound();
        }

        var label = AddressLabelComposer.ParticipantAddress(new AddressLabelFields
        {
            ContactName = participant.ContactName,
            Organisation = participant.Organisation,
            Address1 = participant.Address1,
            Address2 = participant.Address2,
            Address3 = participant.Address3,
            Address4 = participant.Address4,
            Address5 = participant.Address5,
            Country = await CountryNameAsync(participant.CountryId, cancellationToken),
            Telephone = participant.Telephone
        });

        var printed = await labelPrinterApiClient.PrintAsync(label.Lines, cancellationToken);
        TempData.SetNotification(
            printed ? NotificationType.Success : NotificationType.Error,
            printed ? "Label sent to printer." : "There was a problem printing the label. Please try again.");

        return RedirectToAction(nameof(ParticipantAddressLabels), new RouteValueDictionary
        {
            ["searched"] = true,
            ["search.LabCode"] = labCode,
            ["search.LabName"] = labName,
            ["search.ContactName"] = contactName
        });
    }
}
