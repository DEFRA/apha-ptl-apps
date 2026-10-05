using PTL.Contracts.Contract;
using PTL.Contracts.Customer;
using PTL.Contracts.Lookup;

namespace PTL.InternalWeb.Features.Contract;

/// <summary>
/// Everything a document mapper may need. Only the pieces relevant to a given document type are
/// populated by <see cref="ContractController"/>.
/// </summary>
public sealed record ContractDocumentContext(
    string CanonicalDocumentType,
    // Filename of the selected tblUploadedTemplate row actually being merged.
    string TemplateName,
    ContractResponse Contract,
    ContractItemsResponse? Items,
    CustomerResponse? Customer,
    IReadOnlyList<CountryResponse> Countries,
    IReadOnlyList<VatRatingResponse> VatRatings,
    IReadOnlyList<SampleAddressResponse> SampleAddresses,
    ContractRenewalResponse? Renewal);
