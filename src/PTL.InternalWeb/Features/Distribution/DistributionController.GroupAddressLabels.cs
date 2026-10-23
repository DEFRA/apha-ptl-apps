using Microsoft.AspNetCore.Mvc;

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

    // [PRINT FUNCTIONALITY DEFERRED] - legacy prints directly to a Windows printer configured in
    // web.config via System.Drawing.Printing.PrintDocument (PtaBusinessObjects.Labels.LabelBase/
    // LabelGroupAddress, dbo.spgLabelGroupAddress). The target printing architecture for this
    // platform has not yet been agreed, so this action only reaches the shared "not yet migrated"
    // stub - same pattern already used for Contract/Export and Distribution/ParticipantAddressLabels.
    [HttpGet]
    public IActionResult PrintGroupAddressLabel(Guid groupAddressId) =>
        View("FeatureNotAvailable", "Print Group Address Label");
}
