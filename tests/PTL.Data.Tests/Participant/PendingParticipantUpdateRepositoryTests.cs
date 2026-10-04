using System.Data;
using PTL.Core.Participant;
using PTL.Data.Infrastructure;
using PTL.Data.Participant;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Participant;

// Exercises PendingParticipantUpdateRepository against a fake ADO.NET connection (see Fakes/)
// instead of a live SQL Server.
public class PendingParticipantUpdateRepositoryTests
{
    public PendingParticipantUpdateRepositoryTests() => DapperColumnMappings.Register();

    private const string GetSummariesSql = "EXEC dbo.spgaPendingParticipantDetailsEditInfo";
    private const string GetByParticipantIdSql = "EXEC dbo.spgPendingParticipantDetailsEditByParticipantID @ParticipantId, @IsSubmitted";
    private const string MarkDecidedSql = "EXEC dbo.spdPendingParticipantDetailsEditByParticipantID @ParticipantId, @PendingParticipantDetailsEditId";

    private static (PendingParticipantUpdateRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new PendingParticipantUpdateRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    private static DataTable SummaryTable(Guid participantId, Guid pendingId)
    {
        var table = new DataTable();
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldPendingParticipantDetailsEditId", typeof(Guid));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldLabName", typeof(string));
        table.Rows.Add(participantId, pendingId, "001", "Alpha Lab");
        return table;
    }

    private static DataTable PendingTable(Guid participantId, Guid pendingId)
    {
        var table = new DataTable();
        table.Columns.Add("fldPendingParticipantDetailsEditId", typeof(Guid));
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldEmail2", typeof(string));
        table.Columns.Add("fldIsSubmitted", typeof(bool));
        table.Columns.Add("fldIsDeleted", typeof(bool));
        table.Rows.Add(pendingId, participantId, Guid.NewGuid(), "001", "New Contact", "second@example.com", true, false);
        return table;
    }

    [Fact]
    public async Task GetSummariesAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var participantId = Guid.NewGuid();
        connection.RespondToQuery(GetSummariesSql, SummaryTable(participantId, Guid.NewGuid()));

        var result = await repository.GetSummariesAsync();

        Assert.Single(result);
        Assert.Equal(participantId, result[0].ParticipantId);
        Assert.Equal("Alpha Lab", result[0].LabName);
    }

    [Fact]
    public async Task GetByParticipantIdAsync_Found_ReturnsMappedPendingUpdate()
    {
        var (repository, connection) = CreateRepository();
        var participantId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        connection.RespondToQuery(GetByParticipantIdSql, PendingTable(participantId, pendingId));

        var result = await repository.GetByParticipantIdAsync(participantId);

        Assert.NotNull(result);
        Assert.Equal(pendingId, result!.PendingParticipantUpdateId);
        Assert.Equal("New Contact", result.ContactName);
        Assert.Equal("second@example.com", result.Email2);
    }

    [Fact]
    public async Task GetByParticipantIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByParticipantIdSql, new DataTable());

        var result = await repository.GetByParticipantIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task MarkDecidedAsync_PendingUpdateNoLongerOutstanding_ReturnsTrue()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(MarkDecidedSql, -1);
        connection.RespondToQuery(GetByParticipantIdSql, new DataTable());

        var result = await repository.MarkDecidedAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public async Task MarkDecidedAsync_PendingUpdateStillOutstanding_ReturnsFalse()
    {
        var (repository, connection) = CreateRepository();
        var participantId = Guid.NewGuid();
        connection.RespondToNonQuery(MarkDecidedSql, -1);
        connection.RespondToQuery(GetByParticipantIdSql, PendingTable(participantId, Guid.NewGuid()));

        var result = await repository.MarkDecidedAsync(participantId, Guid.NewGuid());

        Assert.False(result);
    }
}
