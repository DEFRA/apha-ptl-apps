using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PTL.Contracts.Contract;
using PTL.Core.Contract;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase - assume the current caller is already authenticated with full
// access to Contract functionality. Policies will be added in a later phase.
[ApiController]
public sealed class ContractController(IContractService contractService, ILogger<ContractController> logger) : ControllerBase
{
    private static readonly Action<ILogger, Guid, Exception?> LogContractNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogContractNotFoundMessage)),
            "Contract {ContractId} not found");

    [HttpGet("api/contracts/{contractId:guid}")]
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
    [HttpGet("api/customers/{customerId:guid}/contracts")]
    public async Task<ActionResult<ContractSearchResponse>> GetContractsForCustomer(
        Guid customerId,
        [FromQuery] ContractSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contractService.SearchContractsAsync(customerId, request.YearId, request.Period, request.SearchTerm, request.Page, request.PageSize, cancellationToken);
        return Ok(new ContractSearchResponse(result.Items.Select(ToSummaryResponse).ToList(), result.TotalCount, request.Page, request.PageSize));
    }

    [HttpPost("api/customers/{customerId:guid}/contracts")]
    public async Task<ActionResult<ContractResponse>> CreateContract(Guid customerId, [FromBody] CreateContractRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await contractService.CreateContractAsync(ToEntity(customerId, request), cancellationToken);
            return CreatedAtAction(nameof(GetContract), new { contractId = created.ContractId }, ToResponse(created));
        }
        catch (ContractValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("api/contracts/{contractId:guid}")]
    public async Task<ActionResult<ContractResponse>> UpdateContract(Guid contractId, [FromBody] UpdateContractRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await contractService.UpdateContractAsync(contractId, ToEntity(contractId, request), cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
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

    private static Contract ToEntity(Guid customerId, CreateContractRequest request) => new()
    {
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

    private static Contract ToEntity(Guid contractId, UpdateContractRequest request) => new()
    {
        ContractId = contractId,
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
}
