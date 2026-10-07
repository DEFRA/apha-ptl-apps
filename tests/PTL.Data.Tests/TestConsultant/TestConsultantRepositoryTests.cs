using System.Data;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using PTL.Data.TestConsultant;
using CoreTestConsultant = PTL.Core.TestConsultant.TestConsultant;

namespace PTL.Data.Tests.TestConsultant;

// Exercises TestConsultantRepository against a fake ADO.NET connection (see Fakes/) instead of a
// live SQL Server - see ParticipantRepositoryTests for the pattern this follows.
public class TestConsultantRepositoryTests
{
    public TestConsultantRepositoryTests() => DapperColumnMappings.Register();

    private const string GetBySsoIdExtSql = "EXEC dbo.spgTestConsultantBySsoId @SsoIdExt=@SsoIdExt";
    private const string GetByEmailSql = "EXEC dbo.spgTestConsultantByEmail @Email";
    private const string InsertSql = "EXEC dbo.spiExtTestConsultant @ExternalTestConsultantId, @Name, @Department, @Email, @SsoId, @IsInactive, @InactiveDate, @SsoIdExt";
    private const string UpdateSql = "EXEC dbo.spuExtTestConsultant @ExternalTestConsultantId, @Name, @Department, @Email, @SsoId, @IsInactive, @InactiveDate, @SsoIdExt";

    private static DataTable TestConsultantTable(Guid externalTestConsultantId, Guid? ssoIdExt = null, string email = "", string name = "Test Consultant")
    {
        var table = new DataTable();
        table.Columns.Add("fldExternalTestConsultantId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldDepartment", typeof(string));
        table.Columns.Add("fldEmail", typeof(string));
        table.Columns.Add("fldSsoId", typeof(Guid));
        table.Columns.Add("fldSsoIdExt", typeof(Guid));
        table.Columns.Add("fldIsInactive", typeof(bool));
        table.Rows.Add(externalTestConsultantId, name, "Dept", email, Guid.NewGuid(), (object?)ssoIdExt ?? DBNull.Value, false);
        return table;
    }

    private static (TestConsultantRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new TestConsultantRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetBySsoIdExtAsync_Found_ReturnsMappedTestConsultant()
    {
        var (repository, connection) = CreateRepository();
        var id = Guid.NewGuid();
        var ssoIdExt = Guid.NewGuid();
        connection.RespondToQuery(GetBySsoIdExtSql, TestConsultantTable(id, ssoIdExt: ssoIdExt));

        var result = await repository.GetBySsoIdExtAsync(ssoIdExt);

        Assert.NotNull(result);
        Assert.Equal(ssoIdExt, result!.SsoIdExt);
    }

    [Fact]
    public async Task GetBySsoIdExtAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetBySsoIdExtSql, new DataTable());

        var result = await repository.GetBySsoIdExtAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEmailAsync_Found_ReturnsMappedTestConsultant()
    {
        var (repository, connection) = CreateRepository();
        var id = Guid.NewGuid();
        const string email = "consultant@example.com";
        connection.RespondToQuery(GetByEmailSql, TestConsultantTable(id, email: email));

        var result = await repository.GetByEmailAsync(email);

        Assert.NotNull(result);
        Assert.Equal(email, result!.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByEmailSql, new DataTable());

        var result = await repository.GetByEmailAsync("missing@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ExecutesInsertAndReturnsTestConsultant()
    {
        var (repository, connection) = CreateRepository();
        var testConsultant = new CoreTestConsultant { ExternalTestConsultantId = Guid.NewGuid(), Name = "New Consultant", Email = "new@example.com", SsoIdExt = Guid.NewGuid() };
        connection.RespondToNonQuery(InsertSql, 1);

        var result = await repository.CreateAsync(testConsultant);

        Assert.Same(testConsultant, result);
        var insertCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSql);
        Assert.Equal(testConsultant.ExternalTestConsultantId, insertCommand.ParameterValue("@ExternalTestConsultantId"));
        Assert.Equal(testConsultant.SsoIdExt, insertCommand.ParameterValue("@SsoIdExt"));
    }

    [Fact]
    public async Task UpdateAsync_ExistingTestConsultant_ReturnsTestConsultant()
    {
        var (repository, connection) = CreateRepository();
        var testConsultant = new CoreTestConsultant { ExternalTestConsultantId = Guid.NewGuid(), Name = "Updated Consultant" };
        connection.RespondToNonQuery(UpdateSql, 1);

        var result = await repository.UpdateAsync(testConsultant);

        Assert.Same(testConsultant, result);
    }

    [Fact]
    public async Task UpdateAsync_UnknownTestConsultant_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var testConsultant = new CoreTestConsultant { ExternalTestConsultantId = Guid.NewGuid() };
        connection.RespondToNonQuery(UpdateSql, 0);

        var result = await repository.UpdateAsync(testConsultant);

        Assert.Null(result);
    }
}
