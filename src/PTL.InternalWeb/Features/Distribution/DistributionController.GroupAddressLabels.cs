using Microsoft.AspNetCore.Mvc;
using PTL.Core.Labels;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.Distribution;

// Group Address Labels - legacy Distributions/GroupAddress.aspx ("This page can be used to print
// single Group Address labels."). Reuses the exact same unpaged, ORDER BY Identifier
// dbo.spgaGroupAddress lookup already built for the Group Address dropdown elsewhere
// (ParticipantSchemeController) - same stored procedure, same "always the latest saved data"
// behaviour (legacy re-fetches on every non-postback load; no caching/snapshotting here either).
public sealed partial class DistributionController
{
    [HttpGet]
    public async Task<IActionResult> GroupAddressLabels(CancellationToken cancellationToken)
    {
        var groupAddresses = await lookupApiClient.GetGroupAddressesAsync(cancellationToken);

        return View(new GroupAddressLabelsViewModel { GroupAddresses = groupAddresses });
    }

    // Legacy GroupAddress.aspx LinkButtonPrint_Click: a fresh GroupAddressInfo fetch by id (not the
    // row already on screen), composed via LabelGroupAddress and sent straight to a Windows printer
    // (PtaBusinessObjects.Labels.LabelBase, web.config LabelPrinterName). Printing is now delegated
    // to PTL.Api (ILabelPrinterApiClient), which holds the actual printer connection - as in legacy,
    // nothing is ever sent to the browser as a file.
    [HttpGet]
    public async Task<IActionResult> PrintGroupAddressLabel(Guid groupAddressId, CancellationToken cancellationToken)
    {
        var groupAddress = await groupAddressApiClient.GetGroupAddressAsync(groupAddressId, cancellationToken);
        if (groupAddress is null)
        {
            return NotFound();
        }

        var label = AddressLabelComposer.GroupAddress(new AddressLabelFields
        {
            Address1 = groupAddress.Address1,
            Address2 = groupAddress.Address2,
            Address3 = groupAddress.Address3,
            Address4 = groupAddress.Address4,
            Address5 = groupAddress.Address5,
            Country = await CountryNameAsync(groupAddress.CountryId, cancellationToken),
            Telephone = groupAddress.Telephone
        });

        var printed = await labelPrinterApiClient.PrintAsync(label.Lines, cancellationToken);
        TempData.SetNotification(
            printed ? NotificationType.Success : NotificationType.Error,
            printed ? "Label sent to printer." : "There was a problem printing the label. Please try again.");
        return RedirectToAction(nameof(GroupAddressLabels));
    }

    // Legacy took the country from a label-specific query that already joins to the country name
    // (dbo.spgLabelGroupAddress) - the lookup list is the faithful equivalent here.
    private async Task<string> CountryNameAsync(Guid countryId, CancellationToken cancellationToken)
    {
        var countries = await lookupApiClient.GetCountriesAsync(cancellationToken);
        return countries.FirstOrDefault(c => c.CountryId == countryId)?.Country ?? string.Empty;
    }
}
