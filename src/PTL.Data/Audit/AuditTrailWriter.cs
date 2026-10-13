using System.Data;
using Dapper;
using PTL.Core.Audit;

namespace PTL.Data.Audit;

// Shared by any repository that must persist a mutation and its audit trail entry atomically in
// one DB transaction (see IUserRoleRepository.ApplyRoleChangesWithAuditAsync /
// IUserRepository.CreateWithAuditAsync). No legacy precedent exists for a generic admin audit
// trail (confirmed gap) - modelled on the one existing transactional-audit precedent in this
// codebase (InvoiceRepository.MarkInvoicedAndRecordAuditAsync: mutation + audit insert, same
// transaction, caller commits/rolls back both together).
internal static class AuditTrailWriter
{
    public static Task WriteAsync(IDbConnection connection, IDbTransaction transaction, AuditEntry entry, CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(
            "EXEC dbo.spiSystemAdministrationAudit @AuditId, @ActingUserId, @Action, @EntityType, @EntityId, @Details",
            new
            {
                AuditId = Guid.NewGuid(),
                entry.ActingUserId,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.Details
            },
            transaction);
}
