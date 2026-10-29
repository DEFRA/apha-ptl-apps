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
    private const string InsertSql = "EXEC dbo.spiPendingParticipantDetailsEdit @PendingParticipantDetailsEditId = @PendingParticipantUpdateId, @ParticipantId = @ParticipantId, @CustomerId = @CustomerId, @LabCode = @LabCode, @ContactName = @ContactName, @Organisation = @Organisation, @Address1 = @Address1, @Address2 = @Address2, @Address3 = @Address3, @Address4 = @Address4, @Address5 = @Address5, @Fax = @Fax, @Email = @Email, @Email2 = @Email2, @Telephone = @Telephone, @CountryId = @CountryId, @IsDeleted = @IsDeleted, @IsSubmitted = @IsSubmitted";
    private const string UpdateSql = "EXEC dbo.spuPendingParticipantDetailsEditByParticipantID @CustomerId = @CustomerId, @ParticipantId = @ParticipantId, @PendingParticipantDetailsEditId = @PendingParticipantUpdateId, @ContactName = @ContactName, @Organisation = @Organisation, @Address1 = @Address1, @Address2 = @Address2, @Address3 = @Address3, @Address4 = @Address4, @Address5 = @Address5, @Telephone = @Telephone, @Fax = @Fax, @Email = @Email, @Email2 = @Email2, @CountryId = @CountryId, @IsSubmitted = @IsSubmitted, @IsDeleted = @IsDeleted";

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

    [Fact]
    public async Task UpsertAsync_NewPendingUpdate_CallsInsertProcedureAndAssignsAnId()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(InsertSql, -1);
        var update = new PendingParticipantUpdate
        {
            ParticipantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            LabCode = "001",
            ContactName = "Jane Doe"
        };

        await repository.UpsertAsync(update);

        Assert.NotEqual(Guid.Empty, update.PendingParticipantUpdateId);
        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(InsertSql, command.CommandText);
        Assert.Equal(update.PendingParticipantUpdateId, (Guid?)command.ParameterValue("PendingParticipantUpdateId"));
        Assert.Equal(update.ParticipantId, (Guid?)command.ParameterValue("ParticipantId"));
        Assert.Equal("Jane Doe", (string?)command.ParameterValue("ContactName"));
    }

    [Fact]
    public async Task UpsertAsync_ExistingPendingUpdateId_CallsUpdateProcedure()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSql, -1);
        var pendingId = Guid.NewGuid();
        var update = new PendingParticipantUpdate
        {
            PendingParticipantUpdateId = pendingId,
            ParticipantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            ContactName = "Jane Doe"
        };

        await repository.UpsertAsync(update);

        Assert.Equal(pendingId, update.PendingParticipantUpdateId);
        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(UpdateSql, command.CommandText);
        Assert.Equal(pendingId, (Guid?)command.ParameterValue("PendingParticipantUpdateId"));
    }

    [Fact]
    public async Task QueueNotificationAsync_InsertsOneEmailRowBoundByName()
    {
        const string queueSql = "EXEC dbo.spiUpdatedParticipantDetailsEmail @UpdatedParticipantDetailsEmailId = @Id, @Email = @Email, @LabCode = @LabCode, @PendingParticipantDetailsEditId = @PendingParticipantUpdateId, @ParticipantName = @ParticipantName, @Notes = @Notes, @IsApproved = @IsApproved";
        var (repository, connection) = CreateRepository();
        var pendingId = Guid.NewGuid();
        connection.RespondToNonQuery(queueSql, -1);

        await repository.QueueNotificationAsync(new ParticipantUpdateNotification("a@example.com", "001", pendingId, "001: Alpha Lab", "Thanks", IsApproved: false));

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(queueSql, command.CommandText);
        Assert.NotEqual(Guid.Empty, (Guid?)command.ParameterValue("Id"));
        Assert.Equal("a@example.com", (string?)command.ParameterValue("Email"));
        Assert.Equal("001", (string?)command.ParameterValue("LabCode"));
        Assert.Equal(pendingId, (Guid?)command.ParameterValue("PendingParticipantUpdateId"));
        Assert.Equal("001: Alpha Lab", (string?)command.ParameterValue("ParticipantName"));
        Assert.Equal("Thanks", (string?)command.ParameterValue("Notes"));
        Assert.Equal(false, (bool?)command.ParameterValue("IsApproved"));
    }

    [Fact]
    public async Task GetUnsentNotificationsAsync_MapsTheRowAndTheLookedUpQalNumber()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldUpdatedParticipantDetailsEmailId", typeof(Guid));
        table.Columns.Add("fldEmail", typeof(string));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldPendingParticipantDetailsEditId", typeof(Guid));
        table.Columns.Add("fldParticipantName", typeof(string));
        table.Columns.Add("fldNotes", typeof(string));
        table.Columns.Add("fldDateSent", typeof(DateTime));
        table.Columns.Add("fldIsApproved", typeof(bool));
        table.Columns.Add("fldQalNumber", typeof(string));
        var id = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        table.Rows.Add(id, "a@example.com", "001", pendingId, "001: Alpha Lab", "Thanks", DBNull.Value, true, "QAL/00001");
        connection.RespondToQuery("EXEC dbo.spgaUpdatedParticipantDetailsEmail", table);

        var result = await repository.GetUnsentNotificationsAsync();

        var row = Assert.Single(result);
        Assert.Equal(id, row.Id);
        Assert.Equal("a@example.com", row.Email);
        Assert.Equal("001", row.LabCode);
        Assert.Equal(pendingId, row.PendingParticipantUpdateId);
        Assert.Equal("001: Alpha Lab", row.ParticipantName);
        Assert.Equal("Thanks", row.Notes);
        Assert.True(row.IsApproved);
        Assert.Equal("QAL/00001", row.QalNumber);
    }

    [Fact]
    public async Task MarkNotificationSentAsync_StampsTheRowBoundByName()
    {
        const string markSql = "EXEC dbo.spuUpdatedParticipantDetailsEmail @UpdatedParticipantDetailsEmailId = @Id, @DateSent = @SentAt";
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
