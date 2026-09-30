using System.Data;
using PTL.Data.Contract.ImportPermit;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Contract;

public class ImportPermitRepositoryTests
{
    public ImportPermitRepositoryTests() => DapperColumnMappings.Register();

    private const string GetSql = "EXEC dbo.spgImportPermitDetailsByContractId @ContractId";
    private const string UpdateSql = "EXEC dbo.spuUpdateImportPermit @ParticipantSchemeId, @ImportPermitReceived, @ImportPermitExpiry";

    private static (ImportPermitRepository Repository, PTL.Data.Tests.Fakes.FakeDbConnection Connection) CreateRepository()
    {
        var connection = new PTL.Data.Tests.Fakes.FakeDbConnection();
        var repository = new ImportPermitRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByContractIdAsync_ReturnsMappedList()
    {
        var (repository, connection) = CreateRepository();
        var participantSchemeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("ParticipantSchemeId", typeof(Guid));
        table.Columns.Add("SchemeNumber", typeof(string));
        table.Columns.Add("SchemeName", typeof(string));
        table.Columns.Add("LabID", typeof(string));
        table.Columns.Add("fldImportExportLicenceRequired", typeof(bool));
        table.Columns.Add("fldImportPermitReceived", typeof(bool));
        table.Columns.Add("fldImportPermitExpiry", typeof(DateTime));
        table.Rows.Add(participantSchemeId, "PT0001", "Salmonella", "1476", true, true, new DateTime(2026, 12, 31));
        connection.RespondToQuery(GetSql, table);

        var result = await repository.GetByContractIdAsync(Guid.NewGuid());

        Assert.Single(result);
        Assert.Equal(participantSchemeId, result[0].ParticipantSchemeId);
        Assert.Equal("PT0001", result[0].SchemeNumber);
        Assert.True(result[0].ImportPermitRequired);
        Assert.True(result[0].ImportPermitReceived);
    }

    [Fact]
    public async Task UpdateAsync_ExecutesNonQuery()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSql, 1);

        await repository.UpdateAsync(Guid.NewGuid(), true, new DateTime(2026, 12, 31));

        Assert.Single(connection.ExecutedCommands.Where(c => c.CommandText == UpdateSql));
    }
}
