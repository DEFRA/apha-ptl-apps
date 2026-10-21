namespace PTL.Contracts.Contract;

// Body of DELETE /api/contracts/{contractId}/items/{participantSchemeId}: the reason the audit dialog captured.
public sealed record ContractItemRemovalRequest(string? AuditWhy);
