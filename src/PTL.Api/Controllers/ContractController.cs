using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PTL.Contracts.Contract;
using PTL.Core.Contract;
using PTL.Core.Contract.Export.Bulk;
using PTL.Core.Contract.Export.Templates;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Contract.PendingOrder;
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
    IPendingOrderService pendingOrderService,
    IExportTemplateService exportTemplateService,
    IBulkExportService bulkExportService,
    ILogger<ContractController> logger) : ControllerBase
{
    private static readonly Action<ILogger, Guid, Exception?> LogContractNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogContractNotFoundMessage)),
            "Contract {ContractId} not found");

    private static readonly Action<ILogger, string, long, long, long, long, long, Exception?> LogUploadTimingMessage =
        LoggerMessage.Define<string, long, long, long, long, long>(
            LogLevel.Information,
            new EventId(2, nameof(LogUploadTimingMessage)),
            "UploadExportTemplate timing for {DocumentType}: FileRead={FileReadMs}ms ServiceCall={ServiceCallMs}ms ResponseBuild={ResponseBuildMs}ms Total={TotalMs}ms (RequestEntryTimestamp={RequestEntryTimestamp})");

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

    [HttpGet("export-templates/{documentType}")]
    public async Task<ActionResult<ExportTemplateListResponse>> GetExportTemplates(string documentType, CancellationToken cancellationToken)
    {
        if (!ExportDocumentTypes.TryResolve(documentType, out var storageName, out var displayName))
        {
            return NotFound();
        }

        var templates = await exportTemplateService.GetTemplatesAsync(storageName, cancellationToken);
        return Ok(new ExportTemplateListResponse(storageName, displayName, [.. templates.Select(ToExportTemplateResponse)]));
    }

    [HttpPost("export-templates/{documentType}")]
    public async Task<ActionResult<ExportTemplateUploadResponse>> UploadExportTemplate(string documentType, IFormFile? file, CancellationToken cancellationToken)
    {
        var requestEntry = Stopwatch.GetTimestamp();
        var total = Stopwatch.StartNew();

        if (!ExportDocumentTypes.TryResolve(documentType, out var storageName, out _))
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            return Ok(new ExportTemplateUploadResponse(false, "File not found", null));
        }

        var fileRead = Stopwatch.StartNew();
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        fileRead.Stop();

        var serviceCall = Stopwatch.StartNew();
        var result = await exportTemplateService.UploadAsync(storageName, file.FileName, buffer.ToArray(), cancellationToken);
        serviceCall.Stop();

        var responseBuild = Stopwatch.StartNew();
        var response = Ok(new ExportTemplateUploadResponse(
            result.Success,
            result.ErrorMessage,
            result.Template is null ? null : ToExportTemplateResponse(result.Template)));
        responseBuild.Stop();

        total.Stop();
        LogUploadTimingMessage(
            logger,
            storageName,
            fileRead.ElapsedMilliseconds,
            serviceCall.ElapsedMilliseconds,
            responseBuild.ElapsedMilliseconds,
            total.ElapsedMilliseconds,
            requestEntry,
            null);

        return response;
    }

    [HttpGet("export-templates/{fileId:guid}/content")]
    public async Task<IActionResult> DownloadExportTemplate(Guid fileId, CancellationToken cancellationToken)
    {
        var content = await exportTemplateService.DownloadAsync(fileId, cancellationToken);
        return content is null ? NotFound() : File(content.Content, content.ContentType, content.FileName);
    }

    [HttpPost("export-templates/{fileId:guid}/select")]
    public async Task<IActionResult> SelectExportTemplate(Guid fileId, CancellationToken cancellationToken) =>
        await exportTemplateService.SelectAsync(fileId, cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("export-templates/{fileId:guid}")]
    public async Task<IActionResult> DeleteExportTemplate(Guid fileId, CancellationToken cancellationToken) =>
        await exportTemplateService.DeleteAsync(fileId, cancellationToken) ? NoContent() : NotFound();

    private static ExportTemplateResponse ToExportTemplateResponse(UploadedTemplate template) =>
        new(template.FileId, template.Filename, template.UploadedDate, template.DocumentType, template.Selected);

    // Bulk export datasets. Legacy loads each in a single call and merges the lot into one document.
    [HttpGet("exports/contracts")]
    public async Task<ActionResult<IReadOnlyList<BulkContractResponse>>> GetBulkContracts(CancellationToken cancellationToken)
    {
        var contracts = await bulkExportService.GetExportableContractsAsync(cancellationToken);
        return Ok(contracts.Select(ToBulkContractResponse).ToList());
    }

    [HttpGet("exports/sample-addresses")]
    public async Task<ActionResult<IReadOnlyList<SampleAddressResponse>>> GetBulkSampleAddresses(CancellationToken cancellationToken)
    {
        var addresses = await bulkExportService.GetSampleAddressesAsync(cancellationToken);
        return Ok(addresses.Select(ToResponse).ToList());
    }

    [HttpGet("exports/renewals")]
    public async Task<ActionResult<IReadOnlyList<ContractRenewalResponse>>> GetBulkRenewals([FromQuery] bool nonUk, CancellationToken cancellationToken)
    {
        var renewals = await bulkExportService.GetRenewalsAsync(nonUk, cancellationToken);
        return Ok(renewals.Select(ToResponse).ToList());
    }

    private static BulkContractResponse ToBulkContractResponse(BulkContractEntity contract) => new(
        contract.ContractId,
        contract.CustomerId,
        contract.ContractNumber,
        contract.Suffix,
        contract.YearId,
        contract.Symbol,
        contract.AdministrationCharge,
        contract.DiscountRate,
        contract.NumberPostage,
        contract.NumberCourier,
        contract.NumberSpecialDelivery,
        contract.PostagePrice,
        contract.CourierPrice,
        contract.SpecialDeliveryPrice,
        contract.CommencementDate,
        contract.QalNumber,
        contract.ContactName,
        contract.Organisation,
        contract.Address1,
        contract.Address2,
        contract.Address3,
        contract.Address4,
        contract.Address5,
        contract.Country,
        contract.Telephone,
        contract.Fax,
        contract.Email,
        contract.InvoiceName,
        contract.InvoiceOrganisation,
        contract.InvoiceAddress1,
        contract.InvoiceAddress2,
        contract.InvoiceAddress3,
        contract.InvoiceAddress4,
        contract.InvoiceAddress5,
        contract.InvoiceCountry,
        contract.InvoiceTelephone,
        contract.InvoiceFax,
        contract.InvoiceEmail,
        contract.AccountNumber,
        contract.VatNumber,
        contract.VatRating,
        contract.PurchaseOrderNumber,
        contract.TotalPriceItems,
        contract.DiscountPrice,
        contract.TotalPrice,
        [.. contract.Items.Select(i => new BulkContractItemResponse(i.Identifier, i.SchemeName, i.LabCode, i.LabName, i.NoOfDistributions, i.Price))]);

    // GET /api/contracts/pending-orders - Review Pending Orders, split by contract year.
    [HttpGet("contracts/pending-orders")]
    public async Task<ActionResult<PendingOrderListResponse>> GetPendingOrders(CancellationToken cancellationToken)
    {
        var (currentYear, nextYear) = await pendingOrderService.GetPendingOrdersAsync(cancellationToken);

        return Ok(new PendingOrderListResponse(
            currentYear.Select(ToPendingOrderSummaryResponse).ToList(),
            nextYear.Select(ToPendingOrderSummaryResponse).ToList()));
    }

    // GET /api/contracts/pending-orders/{pendingContractId} - Pending Order Details.
    [HttpGet("contracts/pending-orders/{pendingContractId:guid}")]
    public async Task<ActionResult<PendingOrderDetailsResponse>> GetPendingOrder(Guid pendingContractId, CancellationToken cancellationToken)
    {
        var detail = await pendingOrderService.GetPendingOrderAsync(pendingContractId, cancellationToken);
        return detail is null ? NotFound() : Ok(ToPendingOrderDetailsResponse(detail));
    }

    // PUT /api/contracts/pending-orders/{pendingContractId}/schemes/{pendingParticipantSchemeId}
    // - the per-row month / Import-Export Licence / Remove edit.
    [HttpPut("contracts/pending-orders/{pendingContractId:guid}/schemes/{pendingParticipantSchemeId:guid}")]
    public async Task<IActionResult> UpdatePendingOrderScheme(
        Guid pendingContractId,
        Guid pendingParticipantSchemeId,
        [FromBody] PendingOrderSchemeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var edit = new PendingOrderSchemeEdit(
            request.DistributionMonthJan, request.DistributionMonthFeb, request.DistributionMonthMar,
            request.DistributionMonthApr, request.DistributionMonthMay, request.DistributionMonthJun,
            request.DistributionMonthJul, request.DistributionMonthAug, request.DistributionMonthSep,
            request.DistributionMonthOct, request.DistributionMonthNov, request.DistributionMonthDec,
            request.ImportExportLicenceRequired, request.IsRemoved);

        var updated = await pendingOrderService.UpdateSchemeAsync(pendingContractId, pendingParticipantSchemeId, edit, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    // POST /api/contracts/pending-orders/{pendingContractId}/approve - creates the contract and its
    // participant schemes, then soft-deletes the pending order.
    [HttpPost("contracts/pending-orders/{pendingContractId:guid}/approve")]
    public async Task<IActionResult> ApprovePendingOrder(Guid pendingContractId, [FromBody] PendingOrderApproveRequest? request, CancellationToken cancellationToken)
    {
        // Authentication is out of scope, so there is no signed-in user to record yet; legacy
        // GetCurrentUser() falls back to "Guest" for the same reason.
        var approvedBy = User?.Identity?.Name is { Length: > 0 } name ? name.ToLowerInvariant() : "Guest";

        var approved = await pendingOrderService.ApprovePendingOrderAsync(
            pendingContractId, request?.PurchaseOrderNumber ?? string.Empty, approvedBy, cancellationToken);
        return approved ? NoContent() : NotFound();
    }

    // POST /api/contracts/pending-orders/{pendingContractId}/decline - discards the pending order
    // without creating a contract.
    [HttpPost("contracts/pending-orders/{pendingContractId:guid}/decline")]
    public async Task<IActionResult> DeclinePendingOrder(Guid pendingContractId, CancellationToken cancellationToken)
    {
        var declined = await pendingOrderService.DeclinePendingOrderAsync(pendingContractId, cancellationToken);
        return declined ? NoContent() : NotFound();
    }

    private static PendingOrderSummaryResponse ToPendingOrderSummaryResponse(PendingOrderSummaryEntity order) => new(
        order.PendingContractId,
        order.CustomerId,
        order.QalNumber,
        order.CustomerName,
        order.YearId,
        order.Year,
        order.OrderSubmitDate);

    private static PendingOrderDetailsResponse ToPendingOrderDetailsResponse(PendingOrderDetail detail) => new(
        detail.Order.PendingContractId,
        detail.Order.CustomerId,
        detail.QalNumber,
        detail.Order.CustomerName,
        detail.Order.YearId,
        detail.Year,
        detail.Order.PurchaseOrderNumber,
        detail.Order.CurrencySymbol,
        detail.Schemes.Select(ToPendingOrderSchemeResponse).ToList(),
        detail.TotalSchemePrice,
        detail.TotalPostagePrice,
        detail.Total);

    private static PendingOrderSchemeResponse ToPendingOrderSchemeResponse(PendingOrderSchemeLine line) => new(
        line.Scheme.PendingParticipantSchemeId,
        line.Scheme.ParticipantId,
        line.Scheme.ParticipantName,
        line.Scheme.SchemeId,
        line.Scheme.SchemeIdentifier,
        line.Scheme.SchemeName,
        line.Months.Select(m => new PendingOrderSchemeMonthResponse(m.Label, m.MonthNumber, m.Selected, m.Enabled, m.AlreadyParticipating)).ToList(),
        line.Scheme.ImportExportLicenceRequired,
        line.Scheme.IsRemoved,
        line.Scheme.Price,
        line.PostagePrice,
        line.TotalPrice);

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
