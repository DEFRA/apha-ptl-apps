using System.Data;
using PTL.Core.Participant;
using PTL.Data.Infrastructure;
using PTL.Data.Participant;
using PTL.Data.Tests.Fakes;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Data.Tests.Participant;

// Exercises ParticipantRepository against a fake ADO.NET connection (see Fakes/) instead of a live
// SQL Server - see ContractRepositoryTests for the pattern this follows.
public class ParticipantRepositoryTests
{
    public ParticipantRepositoryTests() => DapperColumnMappings.Register();

    private const string GetByIdSql = "EXEC dbo.spgParticipantByParticipantId @ParticipantId";
    private const string GetBySsoIdSql = "EXEC dbo.spgParticipantBySsoId @SsoId";
    private const string GetSummariesSql = "EXEC dbo.spgParticipantInfoByCustomerId @CustomerId, @ActiveOnly";
    private const string InsertSql =
        "EXEC dbo.spiParticipant @ParticipantId, @SsoId, @CustomerId, @LabCode, @LabName, @LabTypeId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Fax, @Email, @Email2, @Comments, @IsActive, @InactiveDate, @InactiveError, @InactiveErrorDate";
    private const string UpdateSql =
        "EXEC dbo.spuParticipant @ParticipantId, @SsoId, @CustomerId, @LabCode, @LabName, @LabTypeId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Fax, @Email, @Email2, @Comments, @IsActive, @InactiveDate, @InactiveError, @InactiveErrorDate";

    private static DataTable ParticipantTable(Guid participantId, Guid customerId, Guid? ssoId = null)
    {
        var table = new DataTable();
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldSsoId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldLabName", typeof(string));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Rows.Add(participantId, ssoId ?? Guid.NewGuid(), customerId, "LAB001", "Test Lab", true);
        return table;
    }

    private static (ParticipantRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new ParticipantRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedParticipant()
    {
        var (repository, connection) = CreateRepository();
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, ParticipantTable(participantId, customerId));

        var result = await repository.GetByIdAsync(participantId);

        Assert.NotNull(result);
        Assert.Equal(participantId, result!.ParticipantId);
        Assert.Equal(customerId, result.CustomerId);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByIdSql, new DataTable());

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySsoIdAsync_Found_ReturnsMappedParticipant()
    {
        var (repository, connection) = CreateRepository();
        var participantId = Guid.NewGuid();
        var ssoId = Guid.NewGuid();
        connection.RespondToQuery(GetBySsoIdSql, ParticipantTable(participantId, Guid.NewGuid(), ssoId));

        var result = await repository.GetBySsoIdAsync(ssoId);

        Assert.NotNull(result);
        Assert.Equal(ssoId, result!.SsoId);
    }

    [Fact]
    public async Task GetSummariesAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var customerId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldLabName", typeof(string));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Rows.Add(Guid.NewGuid(), customerId, "LAB001", "Test Lab", "A Contact", true);
        connection.RespondToQuery(GetSummariesSql, table);

        var result = await repository.GetSummariesAsync(customerId);

        Assert.Single(result);
        Assert.Equal(customerId, result[0].CustomerId);
    }

    [Fact]
    public async Task CreateAsync_InsertsAndReReadsParticipant()
    {
        var (repository, connection) = CreateRepository();
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(GetByIdSql, ParticipantTable(participantId, customerId));
        var participant = new CoreParticipant { ParticipantId = participantId, CustomerId = customerId, LabCode = "LAB001", IsActive = true };

        var result = await repository.CreateAsync(participant);

        Assert.Equal(participantId, result.ParticipantId);
        var insertCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSql);
        Assert.Equal(participantId, insertCommand.ParameterValue("@ParticipantId"));
    }

    [Fact]
    public async Task UpdateAsync_ExistingParticipant_UpdatesAndReReads()
    {
        var (repository, connection) = CreateRepository();
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToQuery(GetByIdSql, ParticipantTable(participantId, customerId));
        var participant = new CoreParticipant { ParticipantId = participantId, CustomerId = customerId, LabCode = "LAB001", IsActive = true };

        var result = await repository.UpdateAsync(participant);

        Assert.NotNull(result);
        Assert.Equal(participantId, result!.ParticipantId);
    }

    [Fact]
    public async Task UpdateAsync_UnknownParticipant_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSql, 0);
        var participant = new CoreParticipant { ParticipantId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), LabCode = "LAB001" };

        var result = await repository.UpdateAsync(participant);

        Assert.Null(result);
    }
}
