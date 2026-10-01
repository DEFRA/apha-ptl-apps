namespace PTL.Contracts.Contract;

// One priced participant-scheme line item within a scheme group on the Contract Items page.
// Matches legacy ContractItems.aspx's GridViewParticipants row (Contracts Admin/ContractItems.aspx.vb).
public sealed record ContractItemResponse(
    Guid ParticipantSchemeId,
    Guid ParticipantId,
    string LabCode,
    string LabName,
    string FullName,
    int NumberOfDistributions,
    decimal Price,
    bool NonFeePaying,
    bool HasOverride);
