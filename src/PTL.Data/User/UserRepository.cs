using Dapper;
using PTL.Core.Audit;
using PTL.Core.User;
using PTL.Data.Audit;
using PTL.Data.Infrastructure;
using CoreUser = PTL.Core.User.User;

namespace PTL.Data.User;

// The stored procedures are unchanged legacy objects (spgaUser, spiUser) - this only adds a
// Dapper-based access path to tblUsers.
public sealed class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<IReadOnlyList<CoreUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CoreUser>("EXEC dbo.spgaUser")).ToList();
    }

    public async Task<IReadOnlyList<CoreUser>> GetTestConsultantsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CoreUser>("EXEC dbo.spgaUserTestConsultant")).ToList();
    }

    public Task UpdateDepartmentAsync(Guid userId, string department, bool isInactive, DateTime? inactiveDate, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default) =>
        AuditTrailWriter.ExecuteAsync(
            connectionFactory,
            auditEntry,
            (connection, transaction) => connection.ExecuteAsync(
                "EXEC dbo.spuUserDept @UserId, @Department, @IsInactive, @InactiveDate",
                new { UserId = userId, Department = department, IsInactive = isInactive, InactiveDate = inactiveDate },
                transaction),
            cancellationToken);

    public async Task CreateAsync(CoreUser user, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spiUser @UserId, @Username, @FriendlyName, @FirstName, @LastName, @Email, @Department, @IsInactive, @InactiveDate",
            new { user.UserId, user.Username, user.FriendlyName, user.FirstName, user.LastName, user.Email, user.Department, user.IsInactive, user.InactiveDate });
    }

    public async Task CreateWithAuditAsync(CoreUser user, AuditEntry auditEntry, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(
                "EXEC dbo.spiUser @UserId, @Username, @FriendlyName, @FirstName, @LastName, @Email, @Department, @IsInactive, @InactiveDate",
                new { user.UserId, user.Username, user.FriendlyName, user.FirstName, user.LastName, user.Email, user.Department, user.IsInactive, user.InactiveDate },
                transaction);

            await AuditTrailWriter.WriteAsync(connection, transaction, auditEntry, cancellationToken);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("EXEC dbo.spdUser @UserId", new { UserId = userId });
    }

    public Task DeleteWithAuditAsync(Guid userId, IReadOnlyList<Guid> userRoleIds, AuditEntry auditEntry, CancellationToken cancellationToken = default) =>
        AuditTrailWriter.ExecuteAsync(
            connectionFactory,
            auditEntry,
            async (connection, transaction) =>
            {
                foreach (var userRoleId in userRoleIds)
                {
                    await connection.ExecuteAsync("EXEC dbo.spdUserRole @UserRoleId", new { UserRoleId = userRoleId }, transaction);
                }

                return await connection.ExecuteAsync("EXEC dbo.spdUser @UserId", new { UserId = userId }, transaction);
            },
            cancellationToken);
}
