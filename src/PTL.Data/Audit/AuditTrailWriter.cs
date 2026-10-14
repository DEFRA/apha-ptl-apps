using System.Data;
using Dapper;
using PTL.Core.Audit;
using PTL.Data.Infrastructure;

namespace PTL.Data.Audit;

// Shared by any repository that must persist a mutation and its audit trail entry atomically in
// one DB transaction (see IUserRoleRepository.ApplyRoleChangesWithAuditAsync /
// IUserRepository.CreateWithAuditAsync). No legacy precedent exists for a generic admin audit
// trail (confirmed gap) - modelled on the one existing transactional-audit precedent in this
// codebase (InvoiceRepository.MarkInvoicedAndRecordAuditAsync: mutation + audit insert, same
// transaction, caller commits/rolls back both together).
internal static class AuditTrailWriter
{
    // Runs a repository write and, when an audit entry is supplied, its audit row in ONE
    // transaction: if either fails both roll back, so no unaudited change is ever persisted.
    // With no audit entry it is a plain un-transacted write (system-initiated changes, e.g. the
    // SSO link made at external sign-in, are not System Administration changes).
    public static async Task<T> ExecuteAsync<T>(
        IDbConnectionFactory connectionFactory,
        AuditEntry? auditEntry,
        Func<IDbConnection, IDbTransaction?, Task<T>> change,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        if (auditEntry is null)
        {
            return await change(connection, null);
        }

        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await change(connection, transaction);
            await WriteAsync(connection, transaction, auditEntry, cancellationToken);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

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
