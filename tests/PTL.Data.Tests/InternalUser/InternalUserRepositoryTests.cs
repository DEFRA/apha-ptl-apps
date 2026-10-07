using System.Data;
using PTL.Data.InternalUser;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using CoreInternalUser = PTL.Core.InternalUser.InternalUser;

namespace PTL.Data.Tests.InternalUser;

// Exercises InternalUserRepository against a fake ADO.NET connection (see Fakes/) instead of a
// live SQL Server - see ParticipantRepositoryTests for the pattern this follows. sppAuthenticate
// always returns 2 result sets (user row, then roles), so every response is a DataSet.
public class InternalUserRepositoryTests
{
    public InternalUserRepositoryTests() => DapperColumnMappings.Register();

    private const string GetBySsoIdIntSql = "EXEC dbo.sppAuthenticate @SsoIdInt=@SsoIdInt";
    private const string GetByUsernameSql = "EXEC dbo.sppAuthenticate @Username";
    private const string UpdateSsoIdIntSql = "EXEC dbo.spuUserSsoIdInt @UserId, @SsoIdInt";

    private static DataSet AuthenticateResult(Guid? userId = null, Guid? ssoIdInt = null, string username = "", string friendlyName = "", params string[] roles)
    {
        var userTable = new DataTable();
        userTable.Columns.Add("fldUserId", typeof(Guid));
        userTable.Columns.Add("fldIsInactive", typeof(bool));
        userTable.Columns.Add("fldUsername", typeof(string));
        userTable.Columns.Add("fldFriendlyName", typeof(string));
        userTable.Columns.Add("fldFirstName", typeof(string));
        userTable.Columns.Add("fldLastName", typeof(string));
        userTable.Columns.Add("fldEmail", typeof(string));
        userTable.Columns.Add("fldDepartment", typeof(string));
        userTable.Columns.Add("fldInactiveDate", typeof(DateTime));
        userTable.Columns.Add("fldSsoIdInt", typeof(Guid));

        if (userId.HasValue)
        {
            userTable.Rows.Add(userId.Value, false, username, friendlyName, "", "", "", "", DBNull.Value, (object?)ssoIdInt ?? DBNull.Value);
        }

        var rolesTable = new DataTable();
        rolesTable.Columns.Add("fldRole", typeof(string));
        foreach (var role in roles)
        {
            // fldRole is a fixed-width varchar column in reality - pad to prove TrimEnd() is applied.
            rolesTable.Rows.Add(role.PadRight(20));
        }

        var dataSet = new DataSet();
        dataSet.Tables.Add(userTable);
        dataSet.Tables.Add(rolesTable);
        return dataSet;
    }

    private static (InternalUserRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new InternalUserRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetBySsoIdIntAsync_Found_ReturnsMappedUserWithRoles()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        var ssoIdInt = Guid.NewGuid();
        connection.RespondToQuery(GetBySsoIdIntSql, AuthenticateResult(userId, ssoIdInt, "DEFRA\\auser", "Alice User", "Admin", "Scheme Admin"));

        var result = await repository.GetBySsoIdIntAsync(ssoIdInt);

        Assert.NotNull(result);
        Assert.Equal(userId, result!.UserId);
        Assert.Equal(ssoIdInt, result.SsoIdInt);
        Assert.Equal(["Admin", "Scheme Admin"], result.Roles);
    }

    [Fact]
    public async Task GetBySsoIdIntAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetBySsoIdIntSql, AuthenticateResult());

        var result = await repository.GetBySsoIdIntAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUsernameAsync_Found_ReturnsMappedUser()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        connection.RespondToQuery(GetByUsernameSql, AuthenticateResult(userId, username: "DEFRA\\auser", friendlyName: "Alice User", roles: "Internal User"));

        var result = await repository.GetByUsernameAsync("DEFRA\\auser");

        Assert.NotNull(result);
        Assert.Equal(userId, result!.UserId);
        Assert.Equal("DEFRA\\auser", result.Username);
        Assert.Equal(["Internal User"], result.Roles);
    }

    [Fact]
    public async Task GetByUsernameAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByUsernameSql, AuthenticateResult());

        var result = await repository.GetByUsernameAsync("unknown");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateSsoIdIntAsync_ExecutesUpdate()
    {
        var (repository, connection) = CreateRepository();
        var userId = Guid.NewGuid();
        var ssoIdInt = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSsoIdIntSql, 1);

        await repository.UpdateSsoIdIntAsync(userId, ssoIdInt);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == UpdateSsoIdIntSql);
        Assert.Equal(userId, command.ParameterValue("@UserId"));
        Assert.Equal(ssoIdInt, command.ParameterValue("@SsoIdInt"));
    }
}
