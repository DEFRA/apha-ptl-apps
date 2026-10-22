namespace PTL.Core.Audit;

// One row written to the legacy tblLog (via spiLog), always in the SAME DB transaction as the
// administrative change it describes (see IUserRepository.CreateWithAuditAsync remarks) - if the audit write fails, the whole transaction
// rolls back so no unaudited change is ever persisted.
public sealed record AuditEntry(Guid? ActingUserId, string Action, string EntityType, string? EntityId, string? Details);
