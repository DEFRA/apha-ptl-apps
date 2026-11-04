using System.Data;
using PTL.Data.Contract.ImportPermit;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Contract;

// Legacy spgaContractOrderImportPermitForEmail (customers then participants) and spiImportPermitReminderSent.
public class ImportPermitReminderRepositoryTests
{
    public ImportPermitReminderRepositoryTests() => DapperColumnMappings.Register();

    private const string GetDueSql = "EXEC dbo.spgaContractOrderImportPermitForEmail @DaysBeforeDistribution";
    private const string MarkSentSql = "EXEC dbo.spiImportPermitReminderSent @ContractId, @CustomerId, @MonthlyDistributionSchemeId, @ParticipantId, @ReminderType";

    private static readonly Guid ContractId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid CustomerId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid DistributionId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid ParticipantId = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid ParticipantSchemeId = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid SchemeId = Guid.Parse("66666666-0000-0000-0000-000000000001");
    private static readonly DateTime PostingDate = new(2026, 4, 1);

    private static (ImportPermitReminderRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new ImportPermitReminderRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static DataTable CustomerTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldCustomerName", typeof(string));
        table.Columns.Add("fldCustomerEmail", typeof(string));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Columns.Add("fldDistributionReference", typeof(string));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldCustomerReminderSent", typeof(bool));
        return table;
    }

    private static DataTable ParticipantTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldParticipantSchemeId", typeof(Guid));
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Columns.Add("fldParticipantEmail", typeof(string));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldImportPermitReceived", typeof(bool));
        table.Columns.Add("fldImportPermitExpiry", typeof(DateTime));
        table.Columns.Add("fldDistributionDate", typeof(DateTime));
        table.Columns.Add("fldOverseasPostingDate", typeof(DateTime));
        table.Columns.Add("fldDistributionReference", typeof(string));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        return table;
    }

    private static DataSet DataSetOf(DataTable customers, DataTable participants)
    {
        var dataSet = new DataSet();
        dataSet.Tables.AddRange([customers, participants]);
        return dataSet;
    }

    // ---- GetDueAsync ----

    [Fact]
    public async Task GetDueAsync_MapsBothResultSetsColumnForColumn()
    {
        var (repository, connection) = CreateRepository();
        var customers = CustomerTable();
        customers.Rows.Add(ContractId, CustomerId, "Overseas Labs Ltd", "customer@example.test", "QAL123", "D-001", "PT-900", DistributionId, true);
        var participants = ParticipantTable();
        participants.Rows.Add(
            ContractId, CustomerId, ParticipantSchemeId, ParticipantId, SchemeId, "Salmonella", "QAL123",
            "participant@example.test", "LAB-1", false, PostingDate.AddDays(-5), PostingDate.AddDays(14), PostingDate, "D-001", "PT-900", DistributionId);
        connection.RespondToQuery(GetDueSql, DataSetOf(customers, participants));

        var data = await repository.GetDueAsync(28);

        var customer = Assert.Single(data.Customers);
        Assert.Equal((ContractId, CustomerId, DistributionId), (customer.ContractId, customer.CustomerId, customer.MonthlyDistributionSchemeId));
        Assert.Equal(("Overseas Labs Ltd", "customer@example.test", "QAL123"), (customer.CustomerName, customer.CustomerEmail, customer.QalNumber));
        Assert.Equal(("D-001", "PT-900"), (customer.DistributionReference, customer.SchemeIdentifier));
        Assert.True(customer.ReminderSent);

        var participant = Assert.Single(data.Participants);
        Assert.Equal((ContractId, CustomerId, ParticipantId), (participant.ContractId, participant.CustomerId, participant.ParticipantId));
        Assert.Equal((ParticipantSchemeId, SchemeId, DistributionId), (participant.ParticipantSchemeId, participant.SchemeId, participant.MonthlyDistributionSchemeId));
        Assert.Equal(("Salmonella", "QAL123", "participant@example.test", "LAB-1"), (participant.SchemeName, participant.QalNumber, participant.ParticipantEmail, participant.LabCode));
        Assert.Equal(("D-001", "PT-900"), (participant.DistributionReference, participant.SchemeIdentifier));
        Assert.False(participant.ImportPermitReceived);
        Assert.Equal(PostingDate.AddDays(-5), participant.ImportPermitExpiry);
        Assert.Equal(PostingDate.AddDays(14), participant.DistributionDate);
        Assert.Equal(PostingDate, participant.OverseasPostingDate);
    }

    [Fact]
    public async Task GetDueAsync_PassesTheDayOffsetAsAParameter()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetDueSql, DataSetOf(CustomerTable(), ParticipantTable()));

        await repository.GetDueAsync(14);

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(GetDueSql, command.CommandText);
        Assert.Equal(14, command.ParameterValue("DaysBeforeDistribution"));
    }

    [Fact]
    public async Task GetDueAsync_NoRows_ReturnsEmptyCollections()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetDueSql, DataSetOf(CustomerTable(), ParticipantTable()));

        var data = await repository.GetDueAsync(7);

        Assert.Empty(data.Customers);
        Assert.Empty(data.Participants);
    }

    [Fact]
    public async Task GetDueAsync_NullableDatesStayNull()
    {
        var (repository, connection) = CreateRepository();
        var participants = ParticipantTable();
        participants.Rows.Add(
            ContractId, CustomerId, ParticipantSchemeId, ParticipantId, SchemeId, "Salmonella", "QAL123",
            "participant@example.test", "LAB-1", false, DBNull.Value, DBNull.Value, DBNull.Value, "D-001", "PT-900", DistributionId);
        connection.RespondToQuery(GetDueSql, DataSetOf(CustomerTable(), participants));

        var participant = Assert.Single((await repository.GetDueAsync(28)).Participants);

        Assert.Null(participant.ImportPermitExpiry);
        Assert.Null(participant.DistributionDate);
        Assert.Null(participant.OverseasPostingDate);
    }

    // ---- MarkReminderSentAsync ----

    [Fact]
    public async Task MarkReminderSentAsync_Participant_PassesEveryIdentifier()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(MarkSentSql, 1);

        await repository.MarkReminderSentAsync(ContractId, CustomerId, DistributionId, ParticipantId, 28);

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(MarkSentSql, command.CommandText);
        Assert.Equal(ContractId, command.ParameterValue("ContractId"));
        Assert.Equal(CustomerId, command.ParameterValue("CustomerId"));
        Assert.Equal(DistributionId, command.ParameterValue("MonthlyDistributionSchemeId"));
        Assert.Equal(ParticipantId, command.ParameterValue("ParticipantId"));
        Assert.Equal(28, command.ParameterValue("ReminderType"));
    }

    [Fact]
    public async Task MarkReminderSentAsync_Customer_SendsANullParticipantId()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(MarkSentSql, 1);

        await repository.MarkReminderSentAsync(ContractId, CustomerId, DistributionId, participantId: null, 7);

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(DBNull.Value, command.ParameterValue("ParticipantId"));
        Assert.Equal(7, command.ParameterValue("ReminderType"));
    }
}
