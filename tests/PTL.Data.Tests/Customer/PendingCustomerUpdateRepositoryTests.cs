using System.Data;
using PTL.Core.Customer;
using PTL.Data.Customer;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Customer;

// Exercises PendingCustomerUpdateRepository against a fake ADO.NET connection (see Fakes/)
// instead of a live SQL Server - see CustomerRepositoryTests for the pattern this follows.
public class PendingCustomerUpdateRepositoryTests
{
    public PendingCustomerUpdateRepositoryTests() => DapperColumnMappings.Register();

    private const string GetSummariesSql = "EXEC dbo.spgaPendingCustomerDetailsEditInfo";
    private const string GetByCustomerIdSql = "EXEC dbo.spgPendingCustomerDetailsEditByCustomerID @CustomerId, @IsSubmitted";
    private const string MarkDecidedSql = "EXEC dbo.spdPendingCustomerDetailsEditByCustomerID @CustomerId, @PendingCustomerDetailsEditId";

    private static (PendingCustomerUpdateRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new PendingCustomerUpdateRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    private static DataTable SummaryTable(Guid customerId, Guid pendingId)
    {
        var table = new DataTable();
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldPendingCustomerDetailsEditId", typeof(Guid));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Columns.Add("fldName", typeof(string));
        table.Rows.Add(customerId, pendingId, "QAL0001", "Test Customer");
        return table;
    }

    private static DataTable PendingTable(Guid customerId, Guid pendingId)
    {
        var table = new DataTable();
        table.Columns.Add("fldPendingCustomerDetailsEditId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldIsSubmitted", typeof(bool));
        table.Columns.Add("fldIsDeleted", typeof(bool));
        table.Rows.Add(pendingId, customerId, "New Contact", "New Organisation", true, false);
        return table;
    }

    [Fact]
    public async Task GetSummariesAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var customerId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        connection.RespondToQuery(GetSummariesSql, SummaryTable(customerId, pendingId));

        var result = await repository.GetSummariesAsync();

        Assert.Single(result);
        Assert.Equal(customerId, result[0].CustomerId);
        Assert.Equal("Test Customer", result[0].Name);
    }

    [Fact]
    public async Task GetByCustomerIdAsync_Found_ReturnsMappedPendingUpdate()
    {
        var (repository, connection) = CreateRepository();
        var customerId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        connection.RespondToQuery(GetByCustomerIdSql, PendingTable(customerId, pendingId));

        var result = await repository.GetByCustomerIdAsync(customerId);

        Assert.NotNull(result);
        Assert.Equal(pendingId, result!.PendingCustomerUpdateId);
        Assert.Equal("New Contact", result.ContactName);
    }

    [Fact]
    public async Task GetByCustomerIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByCustomerIdSql, new DataTable());

        var result = await repository.GetByCustomerIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task MarkDecidedAsync_PendingUpdateNoLongerOutstanding_ReturnsTrue()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(MarkDecidedSql, -1);
        connection.RespondToQuery(GetByCustomerIdSql, new DataTable());

        var result = await repository.MarkDecidedAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public async Task MarkDecidedAsync_PendingUpdateStillOutstanding_ReturnsFalse()
    {
        var (repository, connection) = CreateRepository();
        var customerId = Guid.NewGuid();
        connection.RespondToNonQuery(MarkDecidedSql, -1);
        connection.RespondToQuery(GetByCustomerIdSql, PendingTable(customerId, Guid.NewGuid()));

        var result = await repository.MarkDecidedAsync(customerId, Guid.NewGuid());

        Assert.False(result);
    }
}
