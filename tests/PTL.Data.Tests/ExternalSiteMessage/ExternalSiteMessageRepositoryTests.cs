using System.Data;
using PTL.Data.ExternalSiteMessage;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.ExternalSiteMessage;

// Exercises ExternalSiteMessageRepository against a fake ADO.NET connection (see Fakes/) instead
// of a live SQL Server - see PostagePricingPlanRepositoryTests for the pattern this follows.
public class ExternalSiteMessageRepositoryTests
{
    public ExternalSiteMessageRepositoryTests() => DapperColumnMappings.Register();

    private static (ExternalSiteMessageRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new ExternalSiteMessageRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetAsync_ReturnsMappedSingletonRow()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldMessageId", typeof(int));
        table.Columns.Add("fldMessage", typeof(string));
        table.Columns.Add("fldImportantMessage", typeof(string));
        table.Columns.Add("fldSupportEmailAddress", typeof(string));
        table.Rows.Add(0, "<p>Body</p>", "<p>Notice</p>", "vetqas@apha.gov.uk");
        connection.RespondToQuery("EXEC dbo.spgaMainPageMessage", table);

        var result = await repository.GetAsync();

        Assert.Equal("<p>Body</p>", result.Message);
        Assert.Equal("vetqas@apha.gov.uk", result.SupportEmailAddress);
    }

    [Fact]
    public async Task GetAsync_NoRow_Throws()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldMessageId", typeof(int));
        table.Columns.Add("fldMessage", typeof(string));
        table.Columns.Add("fldImportantMessage", typeof(string));
        table.Columns.Add("fldSupportEmailAddress", typeof(string));
        connection.RespondToQuery("EXEC dbo.spgaMainPageMessage", table);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetAsync());
    }

    [Fact]
    public async Task UpdateAsync_ExecutesSpuMainPageMessage()
    {
        var (repository, connection) = CreateRepository();
        const string updateSql = "EXEC dbo.spuMainPageMessage @Message, @ImportantMessage, @SupportEmailAddress";
        connection.RespondToNonQuery(updateSql, 1);

        await repository.UpdateAsync(new PTL.Core.ExternalSiteMessage.ExternalSiteMessage
        {
            Message = "Body",
            ImportantMessage = "Notice",
            SupportEmailAddress = "vetqas@apha.gov.uk"
        });

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == updateSql);
    }
}
