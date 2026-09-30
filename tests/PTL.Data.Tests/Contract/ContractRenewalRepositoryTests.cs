using System.Data;
using PTL.Data.Contract.Renewal;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Contract;

public class ContractRenewalRepositoryTests
{
    public ContractRenewalRepositoryTests() => DapperColumnMappings.Register();

    private const string GetSql = "EXEC dbo.spgaExportContractRenewal";

    private static DataTable RenewalTable(Guid contractId, Guid customerId)
    {
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldQALNumber", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldAddress1", typeof(string));
        table.Columns.Add("fldAddress2", typeof(string));
        table.Columns.Add("fldAddress3", typeof(string));
        table.Columns.Add("fldAddress4", typeof(string));
        table.Columns.Add("fldAddress5", typeof(string));
        table.Columns.Add("fldCountry", typeof(string));
        table.Columns.Add("ContractStartDate", typeof(DateTime));
        table.Columns.Add("ContractEndDate", typeof(DateTime));
        table.Columns.Add("RenewalInformation", typeof(string));
        table.Rows.Add(contractId, customerId, "QAL/00001", "Sample Labs Ltd", "Alice Example",
            "1 Street", "Town", "County", "Country", "Postcode", "United Kingdom",
            new DateTime(2026, 4, 1), new DateTime(2027, 3, 31), "Renewal info");
        return table;
    }

    private static (ContractRenewalRepository Repository, PTL.Data.Tests.Fakes.FakeDbConnection Connection) CreateRepository()
    {
        var connection = new PTL.Data.Tests.Fakes.FakeDbConnection();
        var repository = new ContractRenewalRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByContractIdAsync_Found_ReturnsMatchingEntity()
    {
        var (repository, connection) = CreateRepository();
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToQuery(GetSql, RenewalTable(contractId, customerId));

        var result = await repository.GetByContractIdAsync(contractId);

        Assert.NotNull(result);
        Assert.Equal(contractId, result!.ContractId);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal("Renewal info", result.RenewalInformation);
    }

    [Fact]
    public async Task GetByContractIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSql, RenewalTable(Guid.NewGuid(), Guid.NewGuid()));

        var result = await repository.GetByContractIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
