namespace PTL.Contracts.Contract;

// Public API contract for GET /api/contracts/{contractId}/sample-addresses - one entry per
// (ContractId, ParticipantId), matching legacy SampleAddressContractCollection. Legacy also declares
// an Email2 field, but neither spgExportSampleAddressesByContractId nor spgaExportSampleAddress
// returns that column and no template references it, so it is deliberately not modelled.
public sealed record SampleAddressResponse(
    Guid ContractId,
    Guid ParticipantId,
    string QalNumber,
    string LabCode,
    string ContactName,
    string Organisation,
    string Address1,
    string Address2,
    string Address3,
    string Address4,
    string Address5,
    string Country,
    string Telephone,
    string Fax,
    string Email,
    string VatNumber,
    string AccountNumber,
    string VatRating,
    string PurchaseOrderNumber,
    IReadOnlyList<SampleAddressSchemeResponse> FeePayingSchemes,
    IReadOnlyList<SampleAddressSchemeResponse> NonFeePayingSchemes);
