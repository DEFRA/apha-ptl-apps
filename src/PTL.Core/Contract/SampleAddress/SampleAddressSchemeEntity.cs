namespace PTL.Core.Contract.SampleAddress;

// Mirrors the second and third result sets of spgExportSampleAddressesByContractId. ContractId and
// ParticipantId are carried so rows can be grouped onto their parent address.
public sealed class SampleAddressSchemeEntity
{
    public Guid ContractId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid ParticipantSchemeId { get; set; }
    public string SchemeName { get; set; } = string.Empty;
    public string SchemeIdentifier { get; set; } = string.Empty;
    public string MonthsActive { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
}
