using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PTL.Contracts.Contract;
using PTL.Core.Contract;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase - assume the current caller is already authenticated with full
// access to Contract functionality. Policies will be added in a later phase.
[ApiController]
[Route("api")]
public sealed class ContractController(IContractService contractService, ILogger<ContractController> logger) : ControllerBase
{
    private static readonly Action<ILogger, Guid, Exception?> LogContractNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogContractNotFoundMessage)),
            "Contract {ContractId} not found");

    [HttpGet("contracts/{contractId:guid}")]
    public async Task<ActionResult<ContractResponse>> GetContract(Guid contractId, CancellationToken cancellationToken)
    {
        var contract = await contractService.GetContractAsync(contractId, cancellationToken);
        if (contract is null)
        {
            LogContractNotFoundMessage(logger, contractId, null);
            return NotFound();
        }

        return Ok(ToResponse(contract));
    }

    // GET /api/customers/{customerId}/contracts[?yearId=&period=&searchTerm=&page=&pageSize=]
    // - yearId supplied: exact-year lookup via spgContractInfoByCustomerIdAndYearId (no search/paging).
    // - yearId omitted: spgContractInfoByCustomerId(period) with in-memory search/paging.
    [HttpGet("customers/{customerId:guid}/contracts")]
    public async Task<ActionResult<ContractSearchResponse>> GetContractsForCustomer(
        Guid customerId,
        [FromQuery] ContractSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contractService.SearchContractsAsync(customerId, request.YearId, request.Period, request.SearchTerm, request.Page, request.PageSize, cancellationToken);
        return Ok(new ContractSearchResponse(result.Items.Select(ToSummaryResponse).ToList(), result.TotalCount, request.Page, request.PageSize));
    }

    [HttpPost("customers/{customerId:guid}/contracts")]
    public async Task<ActionResult<ContractResponse>> CreateContract(Guid customerId, [FromBody] ContractRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await contractService.CreateContractAsync(ToEntity(Guid.Empty, customerId, request), cancellationToken);
            return CreatedAtAction(nameof(GetContract), new { contractId = created.ContractId }, ToResponse(created));
        }
        catch (ContractValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("contracts/{contractId:guid}")]
    public async Task<ActionResult<ContractResponse>> UpdateContract(Guid contractId, [FromBody] ContractRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await contractService.UpdateContractAsync(contractId, ToEntity(contractId, Guid.Empty, request), cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (ContractValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    // Aggregated read-model over spgContractItems - see docs/migration/contract-migration.md.
    [HttpGet("contracts/{contractId:guid}/items")]
    public async Task<ActionResult<ContractItemsResponse>> GetContractItems(Guid contractId, CancellationToken cancellationToken)
    {
        var items = await contractService.GetContractItemsAsync(contractId, cancellationToken);
        return items is null ? NotFound() : Ok(ToItemsResponse(items));
    }

    // Explicit REST mutation replacing legacy ContractItems.aspx's deferred "mark removed, save
    // later" flow - see docs/migration/contract-migration.md.
    [HttpDelete("contracts/{contractId:guid}/items/{participantSchemeId:guid}")]
    public async Task<IActionResult> RemoveContractItem(Guid contractId, Guid participantSchemeId, CancellationToken cancellationToken)
    {
        try
        {
            var removed = await contractService.RemoveContractItemAsync(contractId, participantSchemeId, cancellationToken);
            return removed ? NoContent() : NotFound();
        }
        catch (ContractValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    private ActionResult ToValidationProblem(ContractValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static Contract ToEntity(Guid contractId, Guid customerId, ContractRequest request) => new()
    {
        ContractId = contractId,
        CustomerId = customerId,
        YearId = request.YearId,
        UTNumber = request.UTNumber,
        FTNumber = request.FTNumber,
        ContractSignatory = request.ContractSignatory,
        ActionsRequired = request.ActionsRequired,
        RenewalInformation = request.RenewalInformation,
        DiscountRate = request.DiscountRate,
        AdministrationCharge = request.AdministrationCharge,
        NumberCourier = request.NumberCourier,
        CourierPrice = request.CourierPrice,
        NumberPostage = request.NumberPostage,
        PostagePrice = request.PostagePrice,
        NumberSpecialDelivery = request.NumberSpecialDelivery,
        SpecialDeliveryPrice = request.SpecialDeliveryPrice,
        AcknowledgementPostedDate = request.AcknowledgementPostedDate,
        AcknowledgementReturnedDate = request.AcknowledgementReturnedDate,
        JobSheetPostedDate = request.JobSheetPostedDate,
        ReasonForClosure = request.ReasonForClosure,
        DateOfLeaving = request.DateOfLeaving,
        IsActive = request.IsActive,
        Suffix = request.Suffix,
        PurchaseOrderNumber = request.PurchaseOrderNumber,
        OptOutOfInvoiceGeneration = request.OptOutOfInvoiceGeneration,
        IsOnlineOrder = request.IsOnlineOrder
    };

    private static ContractResponse ToResponse(Contract contract) => new(
        contract.ContractId,
        contract.CustomerId,
        contract.CustomerName,
        contract.QalNumber,
        contract.YearId,
        contract.UTNumber,
        contract.FTNumber,
        contract.ContractSignatory,
        contract.ActionsRequired,
        contract.RenewalInformation,
        contract.DiscountRate,
        contract.AdministrationCharge,
        contract.NumberCourier,
        contract.CourierPrice,
        contract.NumberPostage,
        contract.PostagePrice,
        contract.NumberSpecialDelivery,
        contract.SpecialDeliveryPrice,
        contract.AcknowledgementPostedDate,
        contract.AcknowledgementReturnedDate,
        contract.JobSheetPostedDate,
        contract.ReasonForClosure,
        contract.DateOfLeaving,
        contract.IsActive,
        contract.IsReadOnly,
        contract.Suffix,
        contract.CommencementDate,
        contract.PurchaseOrderNumber,
        contract.OptOutOfInvoiceGeneration,
        contract.IsInvoiceSent,
        contract.IsOnlineOrder,
        contract.ApprovedBy,
        contract.ApprovedDate);

    private static ContractSummaryResponse ToSummaryResponse(ContractSummaryEntity contract) => new(
        contract.ContractId,
        contract.CustomerId,
        contract.YearId,
        contract.IsActive,
        contract.Suffix);

    private static ContractItemsResponse ToItemsResponse(ContractItemsAggregate items) => new(
        items.ContractId,
        items.Suffix,
        items.YearId,
        items.QalNumber,
        items.Symbol,
        items.DiscountRate,
        items.AdministrationCharge,
        items.NumberCourier,
        items.CourierPrice,
        items.CourierPriceTotal,
        items.NumberPostage,
        items.PostagePrice,
        items.PostagePriceTotal,
        items.NumberSpecialDelivery,
        items.SpecialDeliveryPrice,
        items.SpecialDeliveryPriceTotal,
        items.DiscountPrice,
        items.TotalPriceItems,
        items.TotalPrice,
        items.IsReadOnly,
        items.Schemes.Select(ToSchemeResponse).ToList());

    private static ContractItemSchemeResponse ToSchemeResponse(ContractItemSchemeGroup scheme) => new(
        scheme.SchemeId,
        scheme.SchemeIdentifier,
        scheme.SchemeName,
        scheme.Participants.Select(ToItemResponse).ToList());

    private static ContractItemResponse ToItemResponse(ContractItemLine item) => new(
        item.ParticipantSchemeId,
        item.ParticipantId,
        item.LabCode,
        item.LabName,
        item.FullName,
        item.NumberOfDistributions,
        item.Price,
        item.NonFeePaying,
        item.HasOverride);
}
