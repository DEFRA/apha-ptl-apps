using System.Data;
using PTL.Core.Search;
using PTL.Data.Infrastructure;
using PTL.Data.Search;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Search;

public class SearchRepositoryTests
{
    public SearchRepositoryTests() => DapperColumnMappings.Register();

    private const string CustomerSql = "EXEC dbo.spgSearchCustomer @QalNumber, @AccountNumber, @Name, @ContactName, @Organisation, @CountryId";
    private const string ParticipantSql = "EXEC dbo.spgSearchParticipant @LabCode, @LabName, @ContactName";

    private static (SearchRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new SearchRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static DataTable CustomerTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Columns.Add("fldAccountNumber", typeof(string));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldCountry", typeof(string));
        return table;
    }

    private static DataTable ParticipantTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldLabName", typeof(string));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldOldLabCode", typeof(string));
        return table;
    }

    [Fact]
    public async Task SearchCustomersAsync_ExecutesTheLegacyProcedureWithEveryParameter()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(CustomerSql, CustomerTable());
        var countryId = Guid.NewGuid();

        await repository.SearchCustomersAsync(new CustomerSearchCriteria("QAL/1", "ACC-1", "Alpha", "Amy", "Org", countryId));

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(CustomerSql, command.CommandText);
        Assert.Equal("QAL/1", command.ParameterValue("@QalNumber"));
        Assert.Equal("ACC-1", command.ParameterValue("@AccountNumber"));
        Assert.Equal("Alpha", command.ParameterValue("@Name"));
        Assert.Equal("Amy", command.ParameterValue("@ContactName"));
        Assert.Equal("Org", command.ParameterValue("@Organisation"));
        Assert.Equal(countryId, command.ParameterValue("@CountryId"));
    }

    [Fact]
    public async Task SearchCustomersAsync_EmptyCriteria_SendsEmptyStringsAndEmptyGuid()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(CustomerSql, CustomerTable());

        await repository.SearchCustomersAsync(new CustomerSearchCriteria(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty));

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(string.Empty, command.ParameterValue("@QalNumber"));
        Assert.Equal(string.Empty, command.ParameterValue("@AccountNumber"));
        Assert.Equal(string.Empty, command.ParameterValue("@Name"));
        Assert.Equal(string.Empty, command.ParameterValue("@ContactName"));
        Assert.Equal(string.Empty, command.ParameterValue("@Organisation"));
        Assert.Equal(Guid.Empty, command.ParameterValue("@CountryId"));
    }

    [Fact]
    public async Task SearchCustomersAsync_MapsEveryColumnAndKeepsTheProcedureOrder()
    {
        var (repository, connection) = CreateRepository();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var table = CustomerTable();
        table.Rows.Add(firstId, "QAL/2", "ACC2", "Zulu", "Zed", "Zulu Org", "France");
        table.Rows.Add(secondId, "QAL/1", "ACC1", "Alpha", "Amy", "Alpha Org", "Spain");
        connection.RespondToQuery(CustomerSql, table);

        var results = await repository.SearchCustomersAsync(new CustomerSearchCriteria(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty));

        Assert.Equal(2, results.Count);
        Assert.Equal(firstId, results[0].CustomerId);
        Assert.Equal("QAL/2", results[0].QalNumber);
        Assert.Equal("ACC2", results[0].AccountNumber);
        Assert.Equal("Zulu", results[0].Name);
        Assert.Equal("Zed", results[0].ContactName);
        Assert.Equal("Zulu Org", results[0].Organisation);
        Assert.Equal("France", results[0].Country);
        Assert.Equal(secondId, results[1].CustomerId);
        Assert.Equal("Spain", results[1].Country);
    }

    [Fact]
    public async Task SearchCustomersAsync_NullColumns_AreMappedAsNull()
    {
        var (repository, connection) = CreateRepository();
        var table = CustomerTable();
        table.Rows.Add(Guid.NewGuid(), "QAL/1", DBNull.Value, "Alpha", DBNull.Value, DBNull.Value, "Spain");
        connection.RespondToQuery(CustomerSql, table);

        var row = Assert.Single(await repository.SearchCustomersAsync(new CustomerSearchCriteria("QAL", string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty)));

        Assert.Null(row.AccountNumber);
        Assert.Null(row.ContactName);
        Assert.Null(row.Organisation);
        Assert.Equal("Alpha", row.Name);
    }

    [Fact]
    public async Task SearchCustomersAsync_NoRows_ReturnsEmptyList()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(CustomerSql, CustomerTable());

        var results = await repository.SearchCustomersAsync(new CustomerSearchCriteria("none", string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty));

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchCustomersAsync_DatabaseFailure_Propagates()
    {
        var (repository, _) = CreateRepository();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SearchCustomersAsync(new CustomerSearchCriteria(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty)));
    }

    [Fact]
    public async Task SearchParticipantsAsync_ExecutesTheLegacyProcedureWithEveryParameter()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(ParticipantSql, ParticipantTable());

        await repository.SearchParticipantsAsync(new ParticipantSearchCriteria("LAB1", "Lab One", "Bob"));

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(ParticipantSql, command.CommandText);
        Assert.Equal("LAB1", command.ParameterValue("@LabCode"));
        Assert.Equal("Lab One", command.ParameterValue("@LabName"));
        Assert.Equal("Bob", command.ParameterValue("@ContactName"));
    }

    [Fact]
    public async Task SearchParticipantsAsync_EmptyCriteria_SendsEmptyStrings()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(ParticipantSql, ParticipantTable());

        await repository.SearchParticipantsAsync(new ParticipantSearchCriteria(string.Empty, string.Empty, string.Empty));

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(string.Empty, command.ParameterValue("@LabCode"));
        Assert.Equal(string.Empty, command.ParameterValue("@LabName"));
        Assert.Equal(string.Empty, command.ParameterValue("@ContactName"));
    }

    [Fact]
    public async Task SearchParticipantsAsync_MapsEveryColumn_AndReturnsOneRowPerOldLabCode()
    {
        var (repository, connection) = CreateRepository();
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var table = ParticipantTable();
        table.Rows.Add(participantId, customerId, "LAB1", "Lab One", "Bob", "OLD1");
        table.Rows.Add(participantId, customerId, "LAB1", "Lab One", "Bob", "OLD2");
        connection.RespondToQuery(ParticipantSql, table);

        var results = await repository.SearchParticipantsAsync(new ParticipantSearchCriteria(string.Empty, string.Empty, string.Empty));

        Assert.Equal(2, results.Count);
        Assert.All(results, row =>
        {
            Assert.Equal(participantId, row.ParticipantId);
            Assert.Equal(customerId, row.CustomerId);
            Assert.Equal("LAB1", row.LabCode);
            Assert.Equal("Lab One", row.LabName);
            Assert.Equal("Bob", row.ContactName);
        });
        Assert.Equal("OLD1", results[0].OldLabCode);
        Assert.Equal("OLD2", results[1].OldLabCode);
    }

    [Fact]
    public async Task SearchParticipantsAsync_NoOldLabCode_LeftJoinNullIsMappedAsNull()
    {
        var (repository, connection) = CreateRepository();
        var table = ParticipantTable();
        table.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "LAB1", "Lab One", DBNull.Value, DBNull.Value);
        connection.RespondToQuery(ParticipantSql, table);

        var row = Assert.Single(await repository.SearchParticipantsAsync(new ParticipantSearchCriteria("LAB", string.Empty, string.Empty)));

        Assert.Null(row.OldLabCode);
        Assert.Null(row.ContactName);
        Assert.Equal("LAB1", row.LabCode);
    }

    [Fact]
    public async Task SearchParticipantsAsync_NoRows_ReturnsEmptyList()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(ParticipantSql, ParticipantTable());

        var results = await repository.SearchParticipantsAsync(new ParticipantSearchCriteria("none", string.Empty, string.Empty));

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchParticipantsAsync_DatabaseFailure_Propagates()
    {
        var (repository, _) = CreateRepository();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SearchParticipantsAsync(new ParticipantSearchCriteria(string.Empty, string.Empty, string.Empty)));
    }
}
