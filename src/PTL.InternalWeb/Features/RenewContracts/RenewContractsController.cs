using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.RenewContracts;

// Legacy MergeContracts.aspx - screen users know as "Renew Contracts".
public class RenewContractsController(
    IContractRenewalApiClient contractRenewalApiClient,
    ICustomerApiClient customerApiClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> RenewContracts(Guid customerId, CancellationToken cancellationToken)
    {
        var model = await BuildModelAsync(customerId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenewContracts(
        Guid customerId,
        List<Guid> selectedContractIds,
        List<Guid> selectedParticipantSchemeIds,
        string? newContractSignatory,
        CancellationToken cancellationToken)
    {
        selectedContractIds ??= [];
        selectedParticipantSchemeIds ??= [];

        var request = new RenewContractRequest(selectedContractIds, selectedParticipantSchemeIds, newContractSignatory);
        var result = await contractRenewalApiClient.RenewContractsAsync(customerId, request, cancellationToken);

        if (!result.Success)
        {
            var model = await BuildModelAsync(customerId, cancellationToken);
            model.SelectedContractIds = selectedContractIds;
            model.SelectedParticipantSchemeIds = selectedParticipantSchemeIds;
            model.NewContractSignatory = newContractSignatory;
            model.ErrorMessage = result.ErrorMessage;
            return View(model);
        }

        return RedirectToAction("Index", "Contract", new { customerId });
    }

    private async Task<RenewContractsViewModel> BuildModelAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await customerApiClient.GetCustomerAsync(customerId, cancellationToken);
        var contracts = await contractRenewalApiClient.GetRenewableContractsAsync(customerId, cancellationToken);
        var items = contracts.IsAllowed
            ? await contractRenewalApiClient.GetRenewableItemsAsync(customerId, cancellationToken)
            : new RenewableContractItemsResponse([]);

        return new RenewContractsViewModel
        {
            CustomerId = customerId,
            CustomerName = customer?.Name ?? string.Empty,
            CustomerOrganisation = customer?.Organisation ?? string.Empty,
            QalNumber = customer?.QalNumber ?? string.Empty,
            IsAllowed = contracts.IsAllowed,
            BlockedReason = contracts.BlockedReason,
            Contracts = contracts.Contracts,
            Items = items.Items,
            ExistingSignatories = contracts.ExistingSignatories,
            SelectedContractIds = contracts.Contracts.Select(c => c.ContractId).ToList(),
            SelectedParticipantSchemeIds = items.Items.Where(i => i.IsRenewable).Select(i => i.ParticipantSchemeId).ToList()
        };
    }
}
