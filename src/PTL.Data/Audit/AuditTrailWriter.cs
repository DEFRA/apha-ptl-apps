using System.Data;
using Dapper;
using PTL.Core.Audit;
using PTL.Data.Infrastructure;

namespace PTL.Data.Audit;

// Shared by any repository that must persist a mutation and its audit trail entry atomically in
// one DB transaction (see IUserRepository.CreateWithAuditAsync). Legacy never audited System Administration changes, so
// the entry is written to the legacy generic log table (tblLog, via spiLog) rather than a new
// table - no schema is added. Modelled on the transactional-audit precedent in this codebase
// (InvoiceRepository.MarkInvoicedAndRecordAuditAsync: mutation + audit insert, same transaction,
// caller commits/rolls back both together).
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

    private const string DescriptionPrefix = "SysAdmin";

    // Legacy tblLog/spiLog take varchar/text, so the strings are passed as ANSI (an nvarchar
    // parameter is not implicitly convertible to a text parameter).
    public static Task WriteAsync(IDbConnection connection, IDbTransaction transaction, AuditEntry entry, CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(
            "EXEC dbo.spiLog @LogId, @LogDate, @UserId, @Description",
            new
            {
                LogId = Guid.NewGuid(),
                LogDate = DateTime.UtcNow,
                UserId = new DbString { Value = entry.ActingUserId?.ToString() ?? "unknown", IsAnsi = true, Length = 250 },
                Description = new DbString { Value = FormatDescription(entry), IsAnsi = true, Length = -1 }
            },
            transaction);

    // tblLog has a single free-text column, so action/entity/id/details are packed into one
    // pipe-delimited line that is easy to filter on: SysAdmin|Action|EntityType|EntityId|Details
    private static string FormatDescription(AuditEntry entry) =>
        string.Join('|', DescriptionPrefix, entry.Action, entry.EntityType, entry.EntityId ?? string.Empty, entry.Details ?? string.Empty);
}
