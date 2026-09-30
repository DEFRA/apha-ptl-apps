using Dapper;
using PTL.Core.Participant;
using PTL.Data.Infrastructure;

namespace PTL.Data.Participant;

// Legacy pending-participant-update stored procedures: spgaPendingParticipantDetailsEditInfo,
// spgPendingParticipantDetailsEditByParticipantID, spdPendingParticipantDetailsEditByParticipantID.
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
}
