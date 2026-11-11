using System.Data;
using Dapper;
using PTL.Core.Audit;
using PTL.Core.LabUsers;
using PTL.Data.Audit;
using PTL.Data.Infrastructure;

namespace PTL.Data.LabUsers;

// Lab user management over spgLabUsersByParticipant / spgLabUserLinkById / spiLabUserForParticipant / spuLabUserForParticipant
// (PTL.Database script 0015). The procedures decide the outcome - uniqueness, sharing and the email rules are enforced
// inside their transactions - and the audit entry is written in that same transaction only when something changed.
public sealed class LabUserRepository(IDbConnectionFactory connectionFactory) : ILabUserRepository
{
    // The audit line names the area it came from, as "SysAdmin" does for System Administration.
    private const string AuditPrefix = "ExternalWeb";

    private sealed record AddRow(int Outcome, Guid? LabUserId);

    public async Task<IReadOnlyList<LabUserLink>> GetByParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var links = await connection.QueryAsync<LabUserLink>(new CommandDefinition(
            "EXEC dbo.spgLabUsersByParticipant @ParticipantId",
            new { ParticipantId = participantId },
            cancellationToken: cancellationToken));
        return links.ToList();
    }

    public async Task<LabUserLink?> GetAsync(Guid participantId, Guid labUserId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<LabUserLink>(new CommandDefinition(
            "EXEC dbo.spgLabUsersByParticipant @ParticipantId, @LabUserId",
            new { ParticipantId = participantId, LabUserId = labUserId },
            cancellationToken: cancellationToken));
    }

    public async Task<LabUserLink?> FindByLinkIdAsync(Guid labUserParticipantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<LabUserLink>(new CommandDefinition(
            "EXEC dbo.spgLabUserLinkById @LabUserParticipantId",
            new { LabUserParticipantId = labUserParticipantId },
            cancellationToken: cancellationToken));
    }

    public async Task<LabUserAddResult> AddAsync(
        Guid participantId, string name, string email, bool isActive, Func<Guid, AuditEntry> auditFor, CancellationToken cancellationToken = default)
    {
        return await InTransactionAsync(async (connection, transaction) =>
        {
            var row = await connection.QuerySingleAsync<AddRow>(new CommandDefinition(
                "EXEC dbo.spiLabUserForParticipant @ParticipantId, @Name, @Email, @IsActive",
                new { ParticipantId = participantId, Name = name, Email = email, IsActive = isActive },
                transaction,
                cancellationToken: cancellationToken));

            var outcome = (LabUserAddOutcome)row.Outcome;
            if (outcome is LabUserAddOutcome.Created or LabUserAddOutcome.AddedExistingAccount && row.LabUserId is { } labUserId)
            {
                await AuditTrailWriter.WriteAsync(connection, transaction, auditFor(labUserId), AuditPrefix, cancellationToken);
            }

            return new LabUserAddResult(outcome, row.LabUserId);
        });
    }

    public async Task<LabUserUpdateOutcome> UpdateAsync(
        Guid participantId, Guid labUserId, string name, string email, bool isActive, bool changeDetails, AuditEntry audit, CancellationToken cancellationToken = default)
    {
        return await InTransactionAsync(async (connection, transaction) =>
        {
            var outcome = (LabUserUpdateOutcome)await connection.QuerySingleAsync<int>(new CommandDefinition(
                "EXEC dbo.spuLabUserForParticipant @ParticipantId, @LabUserId, @Name, @Email, @IsActive, @ChangeDetails",
                new { ParticipantId = participantId, LabUserId = labUserId, Name = name, Email = email, IsActive = isActive, ChangeDetails = changeDetails },
                transaction,
                cancellationToken: cancellationToken));

            if (outcome is LabUserUpdateOutcome.Updated or LabUserUpdateOutcome.UpdatedEmailChanged)
            {
                await AuditTrailWriter.WriteAsync(connection, transaction, audit, AuditPrefix, cancellationToken);
            }

            return outcome;
        });
    }

    // The change and its audit row commit or roll back together, so no change is ever persisted unaudited.
    private async Task<T> InTransactionAsync<T>(Func<IDbConnection, IDbTransaction, Task<T>> work)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await work(connection, transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
