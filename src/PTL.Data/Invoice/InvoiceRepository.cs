using Dapper;
using PTL.Core.Invoice;
using PTL.Data.Infrastructure;

namespace PTL.Data.Invoice;

// The stored procedures are unchanged legacy objects (docs/analysis/invoice-analysis.md) - this
// only adds a Dapper-based access path to them.
public sealed class InvoiceRepository(IDbConnectionFactory connectionFactory) : IInvoiceRepository
{
    public async Task<InvoicePendingData> GetPendingInvoiceDataAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var multi = await connection.QueryMultipleAsync("EXEC dbo.spgaExportContractDetailsForAutomaticInvoicing");

        var contracts = (await multi.ReadAsync<InvoiceContractEntity>()).ToList();
        var items = (await multi.ReadAsync<InvoiceContractItemEntity>()).ToList();

        return new InvoicePendingData(contracts, items);
    }

    public async Task MarkInvoicedAndRecordAuditAsync(string auditWho, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync("EXEC dbo.sppUpdateInvoiceItems", transaction: transaction);
        await connection.ExecuteAsync("EXEC dbo.spiAuditInvoiceGeneration @AuditWho", new { AuditWho = auditWho }, transaction);

        transaction.Commit();
    }

    public async Task ResetInvoicedFlagsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("EXEC dbo.sppResetInvoiceItems");
    }

    public async Task<IReadOnlyList<InvoiceAuditEntity>> GetAuditHistoryAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // [NEEDS INVESTIGATION] no confirmed legacy read procedure exists for
        // tblAuditInvoiceGeneration - see IInvoiceRepository remarks. Columns are aliased directly
        // in the SELECT to match the entity's property names, so no DapperColumnMappings entry is
        // needed for this one-off query.
        return (await connection.QueryAsync<InvoiceAuditEntity>(
            """
            SELECT fldAuditInvoiceGenerationId AS AuditInvoiceGenerationId, fldAuditWho AS AuditWho, fldAuditDate AS AuditDate
            FROM tblAuditInvoiceGeneration
            ORDER BY fldAuditDate DESC
            """)).ToList();
    }
}
