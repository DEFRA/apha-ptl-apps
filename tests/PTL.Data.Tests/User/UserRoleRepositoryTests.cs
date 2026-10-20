using System.Data;
using PTL.Core.Audit;
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
    private const string AuditSql = "EXEC dbo.spiSystemAdministrationAudit @AuditId, @ActingUserId, @Action, @EntityType, @EntityId, @Details";

    [Fact]
    public async Task ApplyRoleChangesWithAuditAsync_CommitsAddsRemovesAndAuditInOneTransaction()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        var roleToAdd = Guid.NewGuid();
        var userRoleToRemove = Guid.NewGuid();
        connection.RespondToNonQuery(InsertRoleSql, 1);
        connection.RespondToNonQuery(DeleteRoleSql, 1);
        connection.RespondToNonQuery(AuditSql, 1);
        var auditEntry = new AuditEntry(userId, "SetUserRoles", "User", userId.ToString(), "Added: [Scheme Admin]");

        await repository.ApplyRoleChangesWithAuditAsync(userId, [roleToAdd], [userRoleToRemove], auditEntry);

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.Committed);
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == InsertRoleSql);
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == DeleteRoleSql);
        var auditCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSql);
        Assert.Equal("SetUserRoles", auditCommand.ParameterValue("@Action"));
    }

    // System Administration business rule: if the audit trail write fails, the change is rolled
    // back - no unaudited role change is ever persisted.
    [Fact]
    public async Task ApplyRoleChangesWithAuditAsync_AuditWriteFails_RollsBackTheTransaction()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertRoleSql, 1);
        // AuditSql deliberately has no configured response - FakeDbConnection throws when a
        // repository executes a command with no configured result, simulating the audit write failing.
        var auditEntry = new AuditEntry(userId, "SetUserRoles", "User", userId.ToString(), "Added: [Scheme Admin]");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ApplyRoleChangesWithAuditAsync(userId, [Guid.NewGuid()], [], auditEntry));

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction!.Committed);
    }
}

