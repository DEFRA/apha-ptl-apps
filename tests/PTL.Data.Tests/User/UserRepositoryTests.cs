using System.Data;
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
}
