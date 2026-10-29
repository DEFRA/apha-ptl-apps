using Dapper;
using PTL.Core.Customer;
using PTL.Data.Infrastructure;

namespace PTL.Data.Customer;

// Legacy pending-customer-update stored procedures: spgaPendingCustomerDetailsEditInfo,
// spgPendingCustomerDetailsEditByCustomerID, spdPendingCustomerDetailsEditByCustomerID; and the email queue:
// spiUpdatedCustomerDetailsEmail, spgaUpdatedCustomerDetailsEmail, spuUpdatedCustomerDetailsEmail.
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

    public async Task UpsertAsync(PendingCustomerUpdate update, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        if (update.PendingCustomerUpdateId == Guid.Empty)
        {
            update.PendingCustomerUpdateId = Guid.NewGuid();

            await connection.ExecuteAsync(
                "EXEC dbo.spiPendingCustomerDetailsEdit @PendingCustomerDetailsEditId = @PendingCustomerUpdateId, @CustomerId = @CustomerId, @ContactName = @ContactName, @Organisation = @Organisation, @Address1 = @Address1, @Address2 = @Address2, @Address3 = @Address3, @Address4 = @Address4, @Address5 = @Address5, @Fax = @Fax, @Email = @Email, @Telephone = @Telephone, @Telephone2 = @Telephone2, @InvoiceName = @InvoiceName, @InvoiceOrganisation = @InvoiceOrganisation, @InvoiceAddress1 = @InvoiceAddress1, @InvoiceAddress2 = @InvoiceAddress2, @InvoiceAddress3 = @InvoiceAddress3, @InvoiceAddress4 = @InvoiceAddress4, @InvoiceAddress5 = @InvoiceAddress5, @InvoiceTelephone = @InvoiceTelephone, @InvoiceTelephone2 = @InvoiceTelephone2, @InvoiceFax = @InvoiceFax, @InvoiceEmail = @InvoiceEmail, @CountryId = @CountryId, @InvoiceCountryId = @InvoiceCountryId, @IsDeleted = @IsDeleted, @IsSubmitted = @IsSubmitted",
                update);

            return;
        }

        await connection.ExecuteAsync(
            "EXEC dbo.spuPendingCustomerDetailsEditByCustomerID @CustomerId = @CustomerId, @PendingCustomerDetailsEditId = @PendingCustomerUpdateId, @ContactName = @ContactName, @Organisation = @Organisation, @Address1 = @Address1, @Address2 = @Address2, @Address3 = @Address3, @Address4 = @Address4, @Address5 = @Address5, @Fax = @Fax, @Email = @Email, @Telephone = @Telephone, @Telephone2 = @Telephone2, @InvoiceName = @InvoiceName, @InvoiceOrganisation = @InvoiceOrganisation, @InvoiceAddress1 = @InvoiceAddress1, @InvoiceAddress2 = @InvoiceAddress2, @InvoiceAddress3 = @InvoiceAddress3, @InvoiceAddress4 = @InvoiceAddress4, @InvoiceAddress5 = @InvoiceAddress5, @InvoiceTelephone = @InvoiceTelephone, @InvoiceTelephone2 = @InvoiceTelephone2, @InvoiceFax = @InvoiceFax, @InvoiceEmail = @InvoiceEmail, @CountryId = @CountryId, @InvoiceCountryId = @InvoiceCountryId, @IsDeleted = @IsDeleted, @IsSubmitted = @IsSubmitted",
            update);
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

    public async Task QueueNotificationAsync(CustomerUpdateNotification notification, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spiUpdatedCustomerDetailsEmail @UpdatedCustomerDetailsEmailId = @Id, @Email = @Email, @PendingCustomerDetailsEditId = @PendingCustomerUpdateId, @CustomerName = @CustomerName, @Notes = @Notes, @IsApproved = @IsApproved",
            new
            {
                Id = Guid.NewGuid(),
                notification.Email,
                notification.PendingCustomerUpdateId,
                notification.CustomerName,
                notification.Notes,
                notification.IsApproved
            });
    }

    public async Task<IReadOnlyList<CustomerUpdateEmailEntity>> GetUnsentNotificationsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<CustomerUpdateEmailEntity>("EXEC dbo.spgaUpdatedCustomerDetailsEmail")).ToList();
    }

    public async Task MarkNotificationSentAsync(Guid notificationId, DateTime sentAt, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spuUpdatedCustomerDetailsEmail @UpdatedCustomerDetailsEmailId = @Id, @DateSent = @SentAt",
            new { Id = notificationId, SentAt = sentAt });
    }
}
