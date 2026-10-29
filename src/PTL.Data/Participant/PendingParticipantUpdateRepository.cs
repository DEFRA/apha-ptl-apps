using Dapper;
using PTL.Core.Participant;
using PTL.Data.Infrastructure;

namespace PTL.Data.Participant;

// Legacy pending-participant-update stored procedures: spgaPendingParticipantDetailsEditInfo,
// spgPendingParticipantDetailsEditByParticipantID, spdPendingParticipantDetailsEditByParticipantID; and the email
// queue: spiUpdatedParticipantDetailsEmail, spgaUpdatedParticipantDetailsEmail, spuUpdatedParticipantDetailsEmail.
public sealed class PendingParticipantUpdateRepository(IDbConnectionFactory connectionFactory) : IPendingParticipantUpdateRepository
{
    public async Task<IReadOnlyList<PendingParticipantUpdateSummaryEntity>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<PendingParticipantUpdateSummaryEntity>(
            "EXEC dbo.spgaPendingParticipantDetailsEditInfo")).ToList();
    }

    public async Task<PendingParticipantUpdate?> GetByParticipantIdAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<PendingParticipantUpdate>(
            "EXEC dbo.spgPendingParticipantDetailsEditByParticipantID @ParticipantId, @IsSubmitted",
            new { ParticipantId = participantId, IsSubmitted = true });
    }

    public async Task UpsertAsync(PendingParticipantUpdate update, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        if (update.PendingParticipantUpdateId == Guid.Empty)
        {
            update.PendingParticipantUpdateId = Guid.NewGuid();

            await connection.ExecuteAsync(
                "EXEC dbo.spiPendingParticipantDetailsEdit @PendingParticipantDetailsEditId = @PendingParticipantUpdateId, @ParticipantId = @ParticipantId, @CustomerId = @CustomerId, @LabCode = @LabCode, @ContactName = @ContactName, @Organisation = @Organisation, @Address1 = @Address1, @Address2 = @Address2, @Address3 = @Address3, @Address4 = @Address4, @Address5 = @Address5, @Fax = @Fax, @Email = @Email, @Email2 = @Email2, @Telephone = @Telephone, @CountryId = @CountryId, @IsDeleted = @IsDeleted, @IsSubmitted = @IsSubmitted",
                update);

            return;
        }

        await connection.ExecuteAsync(
            "EXEC dbo.spuPendingParticipantDetailsEditByParticipantID @CustomerId = @CustomerId, @ParticipantId = @ParticipantId, @PendingParticipantDetailsEditId = @PendingParticipantUpdateId, @ContactName = @ContactName, @Organisation = @Organisation, @Address1 = @Address1, @Address2 = @Address2, @Address3 = @Address3, @Address4 = @Address4, @Address5 = @Address5, @Telephone = @Telephone, @Fax = @Fax, @Email = @Email, @Email2 = @Email2, @CountryId = @CountryId, @IsSubmitted = @IsSubmitted, @IsDeleted = @IsDeleted",
            update);
    }

    public async Task<bool> MarkDecidedAsync(Guid participantId, Guid pendingParticipantUpdateId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // spdPendingParticipantDetailsEditByParticipantID sets NOCOUNT ON, so ExecuteAsync reports
        // -1 rather than a row count. Success is confirmed by re-reading instead - the fetch
        // procedure excludes fldIsDeleted = 1 rows.
        await connection.ExecuteAsync(
            "EXEC dbo.spdPendingParticipantDetailsEditByParticipantID @ParticipantId, @PendingParticipantDetailsEditId",
            new { ParticipantId = participantId, PendingParticipantDetailsEditId = pendingParticipantUpdateId });

        return await GetByParticipantIdAsync(participantId, cancellationToken) is null;
    }

    public async Task QueueNotificationAsync(ParticipantUpdateNotification notification, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spiUpdatedParticipantDetailsEmail @UpdatedParticipantDetailsEmailId = @Id, @Email = @Email, @LabCode = @LabCode, @PendingParticipantDetailsEditId = @PendingParticipantUpdateId, @ParticipantName = @ParticipantName, @Notes = @Notes, @IsApproved = @IsApproved",
            new
            {
                Id = Guid.NewGuid(),
                notification.Email,
                notification.LabCode,
                notification.PendingParticipantUpdateId,
                notification.ParticipantName,
                notification.Notes,
                notification.IsApproved
            });
    }

    public async Task<IReadOnlyList<ParticipantUpdateEmailEntity>> GetUnsentNotificationsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<ParticipantUpdateEmailEntity>("EXEC dbo.spgaUpdatedParticipantDetailsEmail")).ToList();
    }

    public async Task MarkNotificationSentAsync(Guid notificationId, DateTime sentAt, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spuUpdatedParticipantDetailsEmail @UpdatedParticipantDetailsEmailId = @Id, @DateSent = @SentAt",
            new { Id = notificationId, SentAt = sentAt });
    }
}
