using System.Data;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using PTL.Data.User;

namespace PTL.Data.Tests.User;

public class UserRoleRepositoryTests
{
    public UserRoleRepositoryTests() => DapperColumnMappings.Register();

    private static (UserRoleRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new UserRoleRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsMappedAssignments()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userRoleId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldUserRoleId", typeof(Guid));
        table.Columns.Add("fldRoleId", typeof(Guid));
        table.Columns.Add("fldUserId", typeof(Guid));
        table.Rows.Add(userRoleId, roleId, userId);
        connection.RespondToQuery("EXEC dbo.spgUserRoleList @UserId", table);

        var result = await repository.GetForUserAsync(userId);

        var assignment = Assert.Single(result);
        Assert.Equal(roleId, assignment.RoleId);
    }

    [Fact]
    public async Task AddAsync_ExecutesSpiUserRole()
    {
        var (repository, connection) = CreateRepository();
        const string insertSql = "EXEC dbo.spiUserRole @UserRoleId, @UserId, @RoleId";
        connection.RespondToNonQuery(insertSql, 1);

        await repository.AddAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == insertSql);
    }

    [Fact]
    public async Task RemoveAsync_ExecutesSpdUserRole()
    {
        var (repository, connection) = CreateRepository();
        const string deleteSql = "EXEC dbo.spdUserRole @UserRoleId";
        connection.RespondToNonQuery(deleteSql, 1);

        await repository.RemoveAsync(Guid.NewGuid());

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == deleteSql);
    }

    private const string InsertRoleSql = "EXEC dbo.spiUserRole @UserRoleId, @UserId, @RoleId";
    private const string DeleteRoleSql = "EXEC dbo.spdUserRole @UserRoleId";

    [Fact]
    public async Task ApplyRoleChangesAsync_CommitsAddsAndRemovesInOneTransaction()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        var roleToAdd = Guid.NewGuid();
        var userRoleToRemove = Guid.NewGuid();
        connection.RespondToNonQuery(InsertRoleSql, 1);
        connection.RespondToNonQuery(DeleteRoleSql, 1);

        await repository.ApplyRoleChangesAsync(userId, [roleToAdd], [userRoleToRemove]);

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.Committed);
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == InsertRoleSql);
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == DeleteRoleSql);
    }

    // System Administration business rule: if any add/remove in the batch fails, the whole
    // transaction rolls back - no partial role change is ever persisted.
    [Fact]
    public async Task ApplyRoleChangesAsync_AnyWriteFails_RollsBackTheTransaction()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertRoleSql, 1);
        // DeleteRoleSql deliberately has no configured response - FakeDbConnection throws when a
        // repository executes a command with no configured result, simulating that write failing.

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ApplyRoleChangesAsync(userId, [Guid.NewGuid()], [Guid.NewGuid()]));

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction!.Committed);
    }
}

