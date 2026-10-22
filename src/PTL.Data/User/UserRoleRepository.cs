using Dapper;
using PTL.Core.User;
using PTL.Data.Infrastructure;

namespace PTL.Data.User;

// Unchanged legacy stored procedures (spgUserRoleList, spiUserRole, spdUserRole) wrapping
// tlnkUserRoles - no audit trail, matching legacy's UserRoleCollection.Update().
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

    public async Task ApplyRoleChangesAsync(
        Guid userId,
        IReadOnlyList<Guid> roleIdsToAdd,
        IReadOnlyList<Guid> userRoleIdsToRemove,
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

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
