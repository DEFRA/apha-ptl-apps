namespace PTL.Contracts.Contract;

// Wire DTOs for the "Renew Contracts" workflow - legacy MergeContracts.aspx (page title/legacy
// navigation label is "Renew Contracts"; internal class name is Contracts_Admin_MergeContracts).
// Field names mirror legacy ContractMergeInfo/ParticipantSchemeMergeInfo exactly.

public sealed record RenewableContractDto(
    Guid ContractId,
    string Suffix,
    string ContractSignatory,
    string RenewalInformation,
    string ActionsRequired,
    bool IsActive,
    int NoOfItems);

public sealed record RenewableContractItemDto(
    Guid ContractId,
    string Suffix,
    Guid ParticipantSchemeId,
    string LabCode,
    string LabName,
    string OldSchemeIdentifier,
    string OldSchemeName,
    string? NewSchemeIdentifier,
    string? NewSchemeName,
    bool IsRenewable,
    string Identifier);

// GET /api/customers/{customerId}/contracts/renewable-contracts
public sealed record RenewableContractsResponse(
    bool IsAllowed,
    string? BlockedReason,
    IReadOnlyList<string> ExistingSignatories,
    IReadOnlyList<RenewableContractDto> Contracts);

// GET /api/customers/{customerId}/contracts/renewable-items
public sealed record RenewableContractItemsResponse(IReadOnlyList<RenewableContractItemDto> Items);

// POST /api/customers/{customerId}/contracts/renew
public sealed record RenewContractRequest(
    IReadOnlyList<Guid> ContractIds,
    IReadOnlyList<Guid> ParticipantSchemeIds,
    string? NewContractSignatory);

public sealed record RenewContractResponse(bool Success, Guid? NewContractId, string? ErrorMessage);
