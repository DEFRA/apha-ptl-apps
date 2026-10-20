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

    [Fact]
    public async Task QueueNotificationAsync_InsertsOneEmailRowBoundByName()
    {
        const string queueSql = "EXEC dbo.spiUpdatedCustomerDetailsEmail @UpdatedCustomerDetailsEmailId = @Id, @Email = @Email, @PendingCustomerDetailsEditId = @PendingCustomerUpdateId, @CustomerName = @CustomerName, @Notes = @Notes, @IsApproved = @IsApproved";
        var (repository, connection) = CreateRepository();
        var pendingId = Guid.NewGuid();
        connection.RespondToNonQuery(queueSql, -1);

        await repository.QueueNotificationAsync(new CustomerUpdateNotification("a@example.com", pendingId, "Sample Labs", "Thanks", IsApproved: true));

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(queueSql, command.CommandText);
        Assert.NotEqual(Guid.Empty, (Guid?)command.ParameterValue("Id"));
        Assert.Equal("a@example.com", (string?)command.ParameterValue("Email"));
        Assert.Equal(pendingId, (Guid?)command.ParameterValue("PendingCustomerUpdateId"));
        Assert.Equal("Sample Labs", (string?)command.ParameterValue("CustomerName"));
        Assert.Equal("Thanks", (string?)command.ParameterValue("Notes"));
        Assert.Equal(true, (bool?)command.ParameterValue("IsApproved"));
    }

    [Fact]
    public async Task GetUnsentNotificationsAsync_MapsTheRowAndTheLookedUpQalNumberAndLabCode()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldUpdatedCustomerDetailsEmailId", typeof(Guid));
        table.Columns.Add("fldEmail", typeof(string));
        table.Columns.Add("fldPendingCustomerDetailsEditId", typeof(Guid));
        table.Columns.Add("fldCustomerName", typeof(string));
        table.Columns.Add("fldNotes", typeof(string));
        table.Columns.Add("fldDateSent", typeof(DateTime));
        table.Columns.Add("fldIsApproved", typeof(bool));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Columns.Add("fldLabCode", typeof(string));
        var id = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        table.Rows.Add(id, "a@example.com", pendingId, "Sample Labs", "Thanks", DBNull.Value, true, "QAL/00001", "001");
        table.Rows.Add(Guid.NewGuid(), DBNull.Value, pendingId, "Other Labs", "No", DBNull.Value, false, DBNull.Value, DBNull.Value);
        connection.RespondToQuery("EXEC dbo.spgaUpdatedCustomerDetailsEmail", table);

        var result = await repository.GetUnsentNotificationsAsync();

        Assert.Equal(2, result.Count);
        var accepted = Assert.Single(result, r => r.Id == id);
        Assert.Equal("a@example.com", accepted.Email);
        Assert.Equal(pendingId, accepted.PendingCustomerUpdateId);
        Assert.Equal("Sample Labs", accepted.CustomerName);
        Assert.Equal("Thanks", accepted.Notes);
        Assert.True(accepted.IsApproved);
        Assert.Equal("QAL/00001", accepted.QalNumber);
        Assert.Equal("001", accepted.LabCode);
        var declined = Assert.Single(result, r => r.Id != id);
        Assert.Null(declined.Email);
        Assert.Null(declined.QalNumber);
        Assert.False(declined.IsApproved);
    }

    [Fact]
    public async Task MarkNotificationSentAsync_StampsTheRowBoundByName()
    {
        const string markSql = "EXEC dbo.spuUpdatedCustomerDetailsEmail @UpdatedCustomerDetailsEmailId = @Id, @DateSent = @SentAt";
        var (repository, connection) = CreateRepository();
        var id = Guid.NewGuid();
        var sentAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        connection.RespondToNonQuery(markSql, -1);

        await repository.MarkNotificationSentAsync(id, sentAt);

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(markSql, command.CommandText);
        Assert.Equal(id, (Guid?)command.ParameterValue("Id"));
        Assert.Equal(sentAt, (DateTime?)command.ParameterValue("SentAt"));
    }
}
