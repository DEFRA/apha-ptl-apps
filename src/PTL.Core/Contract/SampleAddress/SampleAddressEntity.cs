namespace PTL.Core.Contract.SampleAddress;

// Mirrors the first result set of spgExportSampleAddressesByContractId. Column names are mapped in
// PTL.Data.Infrastructure.DapperColumnMappings - without an entry there every property stays at its
// CLR default with no error.
public sealed class SampleAddressEntity
{
    public Guid ContractId { get; set; }
    public Guid ParticipantId { get; set; }
    public string QalNumber { get; set; } = string.Empty;
    public string LabCode { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Organisation { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Address4 { get; set; } = string.Empty;
    public string Address5 { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string VatRating { get; set; } = string.Empty;
    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public IReadOnlyList<SampleAddressSchemeEntity> FeePayingSchemes { get; set; } = [];
    public IReadOnlyList<SampleAddressSchemeEntity> NonFeePayingSchemes { get; set; } = [];
}
