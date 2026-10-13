using System.Data;
using PTL.Core.Audit;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using PTL.Data.User;

namespace PTL.Data.Tests.User;

// Exercises UserRepository against a fake ADO.NET connection (see Fakes/) instead of a live SQL
// Server - see CountryRepositoryTests for the pattern this follows.
public class UserRepositoryTests
{
    public UserRepositoryTests() => DapperColumnMappings.Register();

    private static (UserRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new UserRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMappedUsers()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldUserId", typeof(Guid));
        table.Columns.Add("fldUsername", typeof(string));
        table.Columns.Add("fldFriendlyName", typeof(string));
        table.Columns.Add("fldFirstName", typeof(string));
        table.Columns.Add("fldLastName", typeof(string));
        table.Columns.Add("fldEmail", typeof(string));
        table.Columns.Add("fldDepartment", typeof(string));
        table.Columns.Add("fldIsInactive", typeof(bool));
        table.Columns.Add("fldInactiveDate", typeof(DateTime));
        table.Rows.Add(userId, "m100001", "Jane Smith", "Jane", "Smith", "jane@apha.gov.uk", "Science", false, DBNull.Value);
        connection.RespondToQuery("EXEC dbo.spgaUser", table);

        var result = await repository.GetAllAsync();

        var user = Assert.Single(result);
        Assert.Equal("m100001", user.Username);
        Assert.Equal("Science", user.Department);
        Assert.False(user.IsInactive);
    }

    [Fact]
    public async Task CreateAsync_ExecutesSpiUser()
    {
        var (repository, connection) = CreateRepository();
        const string insertSql = "EXEC dbo.spiUser @UserId, @Username, @FriendlyName, @FirstName, @LastName, @Email, @Department, @IsInactive, @InactiveDate";
        connection.RespondToNonQuery(insertSql, 1);

        await repository.CreateAsync(new PTL.Core.User.User
        {
            UserId = Guid.NewGuid(),
            Username = "m100001",
            FriendlyName = "Jane Smith",
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@apha.gov.uk",
            Department = "Science",
            IsInactive = false,
            InactiveDate = null
        });

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == insertSql);
    }

    [Fact]
    public async Task DeleteAsync_ExecutesSpdUser()
    {
        var (repository, connection) = CreateRepository();
        const string deleteSql = "EXEC dbo.spdUser @UserId";
        connection.RespondToNonQuery(deleteSql, 1);
        var userId = Guid.NewGuid();

        await repository.DeleteAsync(userId);

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == deleteSql);
    }

    private const string InsertUserSql = "EXEC dbo.spiUser @UserId, @Username, @FriendlyName, @FirstName, @LastName, @Email, @Department, @IsInactive, @InactiveDate";
    private const string AuditSql = "EXEC dbo.spiSystemAdministrationAudit @AuditId, @ActingUserId, @Action, @EntityType, @EntityId, @Details";

    [Fact]
    public async Task CreateWithAuditAsync_CommitsTheInsertAndAuditInOneTransaction()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertUserSql, 1);
        connection.RespondToNonQuery(AuditSql, 1);
        var auditEntry = new AuditEntry(null, "CreateUser", "User", userId.ToString(), "Username: m100001");

        await repository.CreateWithAuditAsync(
            new PTL.Core.User.User { UserId = userId, Username = "m100001", FriendlyName = "Jane Smith", FirstName = "Jane", LastName = "Smith", Email = "jane@apha.gov.uk", Department = "Science" },
            auditEntry);

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.Committed);
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == InsertUserSql);
        var auditCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSql);
        Assert.Equal("CreateUser", auditCommand.ParameterValue("@Action"));
    }

    // System Administration business rule: if the audit trail write fails, the account creation
    // is rolled back - no unaudited change is ever persisted.
    [Fact]
    public async Task CreateWithAuditAsync_AuditWriteFails_RollsBackTheTransaction()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertUserSql, 1);
        // AuditSql deliberately has no configured response - simulates the audit write failing.
        var auditEntry = new AuditEntry(null, "CreateUser", "User", userId.ToString(), "Username: m100001");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateWithAuditAsync(
            new PTL.Core.User.User { UserId = userId, Username = "m100001", FriendlyName = "Jane Smith", FirstName = "Jane", LastName = "Smith", Email = "jane@apha.gov.uk", Department = "Science" },
            auditEntry));

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task GetTestConsultantsAsync_ReturnsMappedUsers()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldUserId", typeof(Guid));
        table.Columns.Add("fldUsername", typeof(string));
        table.Columns.Add("fldFriendlyName", typeof(string));
        table.Columns.Add("fldFirstName", typeof(string));
        table.Columns.Add("fldLastName", typeof(string));
        table.Columns.Add("fldEmail", typeof(string));
        table.Columns.Add("fldDepartment", typeof(string));
        table.Columns.Add("fldIsInactive", typeof(bool));
        table.Columns.Add("fldInactiveDate", typeof(DateTime));
        table.Rows.Add(userId, "m100001", "Jane Smith", "Jane", "Smith", "jane@apha.gov.uk", "Science", false, DBNull.Value);
        connection.RespondToQuery("EXEC dbo.spgaUserTestConsultant", table);

        var result = await repository.GetTestConsultantsAsync();

        Assert.Equal("Jane Smith", Assert.Single(result).FriendlyName);
    }

    [Fact]
    public async Task UpdateDepartmentAsync_ExecutesSpuUserDept()
    {
        var (repository, connection) = CreateRepository();
        const string updateSql = "EXEC dbo.spuUserDept @UserId, @Department, @IsInactive, @InactiveDate";
        connection.RespondToNonQuery(updateSql, 1);
        var userId = Guid.NewGuid();

        await repository.UpdateDepartmentAsync(userId, "Science", true, new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc));

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == updateSql);
    }
}
