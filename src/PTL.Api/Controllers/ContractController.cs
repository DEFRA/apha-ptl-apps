using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PTL.Contracts.Contract;
using PTL.Core.Contract;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Contract.Renew;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase - assume the current caller is already authenticated with full
// access to Contract functionality. Policies will be added in a later phase.
[ApiController]
[Route("api")]
public sealed class ContractController(
    IContractService contractService,
    IImportPermitService importPermitService,
    ISampleAddressService sampleAddressService,
    IContractRenewalService contractRenewalService,
    IRenewContractsService renewContractsService,
    ILogger<ContractController> logger) : ControllerBase
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

    // GET /api/contracts/{contractId}/import-permits - see ImportPermit.aspx.
    [HttpGet("contracts/{contractId:guid}/import-permits")]
    public async Task<ActionResult<IReadOnlyList<ImportPermitResponse>>> GetImportPermits(Guid contractId, CancellationToken cancellationToken)
    {
        var permits = await importPermitService.GetByContractIdAsync(contractId, cancellationToken);
        return Ok(permits.Select(ToResponse).ToList());
    }

    // PUT /api/contracts/import-permits/{participantSchemeId} - matches legacy btnApply_Click/
    // btnSave_Click looping ImportPermitDataAccess.UpdateImportPermit per row.
    [HttpPut("contracts/import-permits/{participantSchemeId:guid}")]
    public async Task<IActionResult> UpdateImportPermit(Guid participantSchemeId, [FromBody] UpdateImportPermitRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await importPermitService.UpdateAsync(participantSchemeId, request.ImportPermitReceived, request.ImportPermitExpiry, cancellationToken);
            return NoContent();
        }
        catch (ImportPermitValidationException ex)
        {
            ModelState.AddModelError(nameof(request.ImportPermitExpiry), ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    // GET /api/contracts/{contractId}/sample-addresses - legacy SampleAddressContractCollection,
    // one entry per participant sample address on the contract.
    [HttpGet("contracts/{contractId:guid}/sample-addresses")]
    public async Task<ActionResult<IReadOnlyList<SampleAddressResponse>>> GetSampleAddresses(Guid contractId, CancellationToken cancellationToken)
    {
        var addresses = await sampleAddressService.GetByContractIdAsync(contractId, cancellationToken);
        return Ok(addresses.Select(ToResponse).ToList());
    }

    // GET /api/contracts/{contractId}/renewal - legacy ContractRenewalCollection.GetByContractId.
    [HttpGet("contracts/{contractId:guid}/renewal")]
    public async Task<ActionResult<ContractRenewalResponse>> GetRenewal(Guid contractId, CancellationToken cancellationToken)
    {
        var renewal = await contractRenewalService.GetByContractIdAsync(contractId, cancellationToken);
        return renewal is null ? NotFound() : Ok(ToResponse(renewal));
    }

    // Legacy MergeContracts.aspx - the screen users know as "Renew Contracts".
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

    private static ImportPermitResponse ToResponse(ImportPermitEntity entity) => new(
        entity.ParticipantSchemeId,
        entity.SchemeNumber,
        entity.SchemeName,
        entity.LabId,
        entity.ImportPermitRequired,
        entity.ImportPermitReceived,
        entity.ImportPermitExpiry);

    private static SampleAddressResponse ToResponse(SampleAddressEntity address) => new(
        address.ContractId,
        address.ParticipantId,
        address.QalNumber,
        address.LabCode,
        address.ContactName,
        address.Organisation,
        address.Address1,
        address.Address2,
        address.Address3,
        address.Address4,
        address.Address5,
        address.Country,
        address.Telephone,
        address.Fax,
        address.Email,
        address.VatNumber,
        address.AccountNumber,
        address.VatRating,
        address.PurchaseOrderNumber,
        address.FeePayingSchemes.Select(ToResponse).ToList(),
        address.NonFeePayingSchemes.Select(ToResponse).ToList());

    private static SampleAddressSchemeResponse ToResponse(SampleAddressSchemeEntity scheme) => new(
        scheme.ParticipantSchemeId,
        scheme.SchemeName,
        scheme.SchemeIdentifier,
        scheme.MonthsActive,
        scheme.WeekNumber);

    private static ContractRenewalResponse ToResponse(ContractRenewalEntity renewal) => new(
        renewal.ContractId,
        renewal.CustomerId,
        renewal.QalNumber,
        renewal.OrganisationName,
        renewal.ContactName,
        renewal.Address1,
        renewal.Address2,
        renewal.Address3,
        renewal.Address4,
        renewal.Address5,
        renewal.Country,
        renewal.ContractStartDate,
        renewal.ContractEndDate,
        renewal.RenewalInformation);

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
