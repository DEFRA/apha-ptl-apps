using System.Data;
using PTL.Data.Infrastructure;
using PTL.Data.SystemMessage;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.SystemMessage;

// Exercises SystemMessageRepository against a fake ADO.NET connection (see Fakes/) instead of a
// live SQL Server - see LookupRepositoryTests for the pattern this follows.
public class SystemMessageRepositoryTests
{
    private static (SystemMessageRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new SystemMessageRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetImportantMessageAsync_MessagePublished_ReturnsMessage()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldMessageId", typeof(int));
        table.Columns.Add("fldMessage", typeof(string));
        table.Columns.Add("fldImportantMessage", typeof(string));
        table.Columns.Add("fldSupportEmailAddress", typeof(string));
        table.Rows.Add(0, "Welcome message", "<p>Planned maintenance <strong>Friday</strong></p>", "support@example.com");
        connection.RespondToQuery("EXEC dbo.spgMainPageMessageBySsoId @SsoId", table);

        var result = await repository.GetImportantMessageAsync();

        Assert.Equal("<p>Planned maintenance <strong>Friday</strong></p>", result);
    }

    [Fact]
    public async Task GetImportantMessageAsync_NoRowReturned_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery("EXEC dbo.spgMainPageMessageBySsoId @SsoId", new DataTable());

        var result = await repository.GetImportantMessageAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetImportantMessageAsync_MessageClearedToNull_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldMessageId", typeof(int));
        table.Columns.Add("fldMessage", typeof(string));
        table.Columns.Add("fldImportantMessage", typeof(string));
        table.Columns.Add("fldSupportEmailAddress", typeof(string));
        table.Rows.Add(0, "Welcome message", DBNull.Value, "support@example.com");
        connection.RespondToQuery("EXEC dbo.spgMainPageMessageBySsoId @SsoId", table);

        var result = await repository.GetImportantMessageAsync();

        Assert.Null(result);
    }
}
