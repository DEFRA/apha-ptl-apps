using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization - see ContractController's identical note.
[ApiController]
[Route("api")]
public sealed class ContractExportController(
    ISampleAddressService sampleAddressService,
    IContractRenewalService contractRenewalService) : ControllerBase
{
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
}
