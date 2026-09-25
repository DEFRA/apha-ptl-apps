namespace PTL.Contracts.Contract;

// Outcome of DELETE /api/contracts/{contractId}/items/{participantSchemeId} - lets InternalWeb
// distinguish "not found" from a business-rule rejection (e.g. contract IsReadOnly) without relying
// on an unhandled HTTP exception.
public sealed record ContractItemRemovalResult(bool Success, bool NotFound, string? ErrorMessage);
