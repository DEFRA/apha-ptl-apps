using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Renew;

namespace PTL.Api.Controllers;

// Legacy MergeContracts.aspx - the screen users know as "Renew Contracts".
[ApiController]
[Route("api")]
public sealed class ContractRenewalController(IRenewContractsService renewContractsService) : ControllerBase
{
    [HttpGet("customers/{customerId:guid}/contracts/renewable-contracts")]
    public async Task<ActionResult<RenewableContractsResponse>> GetRenewableContracts(Guid customerId, CancellationToken cancellationToken)
    {
        var result = await renewContractsService.GetRenewableContractsAsync(customerId, cancellationToken);
        return Ok(new RenewableContractsResponse(
            result.Eligibility.IsAllowed,
            result.Eligibility.BlockedReason,
            result.ExistingSignatories,
            result.Contracts.Select(ToDto).ToList()));
    }

    [HttpGet("customers/{customerId:guid}/contracts/renewable-items")]
    public async Task<ActionResult<RenewableContractItemsResponse>> GetRenewableItems(Guid customerId, CancellationToken cancellationToken)
    {
        var items = await renewContractsService.GetRenewableItemsAsync(customerId, cancellationToken);
        return Ok(new RenewableContractItemsResponse(items.Select(ToDto).ToList()));
    }

    [HttpPost("customers/{customerId:guid}/contracts/renew")]
    public async Task<ActionResult<RenewContractResponse>> RenewContracts(Guid customerId, [FromBody] RenewContractRequest request, CancellationToken cancellationToken)
    {
        var result = await renewContractsService.RenewContractsAsync(customerId, request.ContractIds, request.ParticipantSchemeIds, request.NewContractSignatory, cancellationToken);
        return Ok(new RenewContractResponse(result.Success, result.NewContractId, result.ErrorMessage));
    }

    private static RenewableContractDto ToDto(RenewableContractEntity contract) => new(
        contract.ContractId,
        contract.Suffix,
        contract.ContractSignatory,
        contract.RenewalInformation,
        contract.ActionsRequired,
        contract.IsActive,
        contract.NoOfItems);

    private static RenewableContractItemDto ToDto(RenewableContractItemEntity item) => new(
        item.ContractId,
        item.Suffix,
        item.ParticipantSchemeId,
        item.LabCode,
        item.LabName,
        item.OldSchemeIdentifier,
        item.OldSchemeName,
        item.NewSchemeIdentifier,
        item.NewSchemeName,
        item.IsRenewable,
        item.Identifier);
}
