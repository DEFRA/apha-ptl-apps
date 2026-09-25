namespace PTL.Contracts.Contract;

// One scheme grouping on the Contract Items page - matches legacy ContractItems.aspx's outer
// GridViewContractItems row (scheme header + nested GridViewParticipants).
public sealed record ContractItemSchemeResponse(
    Guid SchemeId,
    string SchemeIdentifier,
    string SchemeName,
    IReadOnlyList<ContractItemResponse> Participants);
