using Dapper;
using PTL.Core.Audit;
using PTL.Core.User;
using PTL.Data.Audit;
using PTL.Data.Infrastructure;

namespace PTL.Data.User;

// Unchanged legacy stored procedures (spgUserRoleList, spiUserRole, spdUserRole) wrapping
// tlnkUserRoles. Role changes are also logged to the legacy tblLog (via spiLog, see AuditTrailWriter) in
// the same transaction - no table or stored procedure is added.
public sealed class UserRoleRepository(IDbConnectionFactory connectionFactory) : IUserRoleRepository
{
    public async Task<IReadOnlyList<UserRoleAssignment>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<UserRoleAssignment>("EXEC dbo.spgUserRoleList @UserId", new { UserId = userId })).ToList();
    }

    public async Task AddAsync(Guid userRoleId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spiUserRole @UserRoleId, @UserId, @RoleId",
            new { UserRoleId = userRoleId, UserId = userId, RoleId = roleId });
    }

    public async Task RemoveAsync(Guid userRoleId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("EXEC dbo.spdUserRole @UserRoleId", new { UserRoleId = userRoleId });
    }

    public async Task ApplyRoleChangesWithAuditAsync(
        Guid userId,
        IReadOnlyList<Guid> roleIdsToAdd,
        IReadOnlyList<Guid> userRoleIdsToRemove,
        AuditEntry auditEntry,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var roleId in roleIdsToAdd)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spiUserRole @UserRoleId, @UserId, @RoleId",
                    new { UserRoleId = Guid.NewGuid(), UserId = userId, RoleId = roleId },
                    transaction);
            }

            foreach (var userRoleId in userRoleIdsToRemove)
            {
                await connection.ExecuteAsync("EXEC dbo.spdUserRole @UserRoleId", new { UserRoleId = userRoleId }, transaction);
            }

            await AuditTrailWriter.WriteAsync(connection, transaction, auditEntry, cancellationToken);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
