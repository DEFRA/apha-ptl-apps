using Dapper;
using PTL.Core.Customer;
using PTL.Data.Infrastructure;

namespace PTL.Data.Customer;

// Legacy pending-customer-update stored procedures: spgaPendingCustomerDetailsEditInfo,
// spgPendingCustomerDetailsEditByCustomerID, spdPendingCustomerDetailsEditByCustomerID.
public sealed class PendingCustomerUpdateRepository(IDbConnectionFactory connectionFactory) : IPendingCustomerUpdateRepository
{
    public async Task<IReadOnlyList<PendingCustomerUpdateSummaryEntity>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<PendingCustomerUpdateSummaryEntity>(
            "EXEC dbo.spgaPendingCustomerDetailsEditInfo")).ToList();
    }

    public async Task<PendingCustomerUpdate?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<PendingCustomerUpdate>(
            "EXEC dbo.spgPendingCustomerDetailsEditByCustomerID @CustomerId, @IsSubmitted",
            new { CustomerId = customerId, IsSubmitted = true });
    }

    public async Task<bool> MarkDecidedAsync(Guid customerId, Guid pendingCustomerUpdateId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // spdPendingCustomerDetailsEditByCustomerID sets NOCOUNT ON, so ExecuteAsync reports -1
        // rather than a row count. Success is confirmed by re-reading instead - the fetch
        // procedure excludes fldIsDeleted = 1 rows.
        await connection.ExecuteAsync(
            "EXEC dbo.spdPendingCustomerDetailsEditByCustomerID @CustomerId, @PendingCustomerDetailsEditId",
            new { CustomerId = customerId, PendingCustomerDetailsEditId = pendingCustomerUpdateId });

        return await GetByCustomerIdAsync(customerId, cancellationToken) is null;
    }
}
