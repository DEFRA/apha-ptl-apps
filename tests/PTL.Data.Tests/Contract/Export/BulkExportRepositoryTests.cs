using System.Data;
using PTL.Data.Contract.Export;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Contract.Export;

// Exercises BulkExportRepository against a fake ADO.NET connection (see Fakes/) - see
// ContractRepositoryTests for the pattern this follows.
public class BulkExportRepositoryTests
{
    public BulkExportRepositoryTests() => DapperColumnMappings.Register();

    private const string GetContractsSql = "EXEC dbo.spgaExportContractDetails";
    private const string GetSampleAddressesSql = "EXEC dbo.spgaExportSampleAddress";
    private const string GetRenewalsSql = "EXEC dbo.spgExportContractRenewal @NonUk";

    private static (BulkExportRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new BulkExportRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    private static DataTable ContractsTable(Guid contractId)
    {
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldContractNumber", typeof(string));
        table.Columns.Add("fldYearId", typeof(int));
        table.Rows.Add(contractId, "QAL/00001", 2026);
        return table;
    }

    private static DataTable ContractItemsTable(Guid contractId, decimal price)
    {
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldPrice", typeof(decimal));
        table.Rows.Add(contractId, "LAB1", price);
        return table;
    }

    [Fact]
    public async Task GetContractsAsync_GroupsItemsUnderMatchingContract_AndDefaultsToEmptyForOthers()
    {
        var (repository, connection) = CreateRepository();
        var contractWithItems = Guid.NewGuid();
        var contractWithoutItems = Guid.NewGuid();
        var contracts = ContractsTable(contractWithItems);
        contracts.Rows.Add(contractWithoutItems, "QAL/00002", 2026);
        var dataSet = new DataSet();
        dataSet.Tables.Add(contracts);
        dataSet.Tables.Add(ContractItemsTable(contractWithItems, 42.5m));
        connection.RespondToQuery(GetContractsSql, dataSet);

        var result = await repository.GetContractsAsync();

        Assert.Equal(2, result.Count);
        var withItems = result.Single(c => c.ContractId == contractWithItems);
        var withoutItems = result.Single(c => c.ContractId == contractWithoutItems);
        var item = Assert.Single(withItems.Items);
        Assert.Equal("LAB1", item.LabCode);
        Assert.Empty(withoutItems.Items);
    }

    [Fact]
    public async Task GetContractsAsync_NoContracts_ReturnsEmptyList()
    {
        var (repository, connection) = CreateRepository();
        var contracts = ContractsTable(Guid.NewGuid());
        contracts.Rows.Clear();
        var items = ContractItemsTable(Guid.NewGuid(), 0m);
        items.Rows.Clear();
        var dataSet = new DataSet();
        dataSet.Tables.Add(contracts);
        dataSet.Tables.Add(items);
        connection.RespondToQuery(GetContractsSql, dataSet);

        var result = await repository.GetContractsAsync();

        Assert.Empty(result);
    }

    private static DataSet SampleAddressDataSet(Guid contractId, Guid matchedParticipantId, Guid unmatchedParticipantId, string? monthsActive)
    {
        var addresses = new DataTable();
        addresses.Columns.Add("fldContractId", typeof(Guid));
        addresses.Columns.Add("fldParticipantId", typeof(Guid));
        addresses.Columns.Add("fldLabCode", typeof(string));
        addresses.Rows.Add(contractId, matchedParticipantId, "LAB1");
        addresses.Rows.Add(contractId, unmatchedParticipantId, "LAB2");

        var feePaying = new DataTable();
        feePaying.Columns.Add("fldContractId", typeof(Guid));
        feePaying.Columns.Add("fldParticipantId", typeof(Guid));
        feePaying.Columns.Add("fldIdentifier", typeof(string));
        feePaying.Columns.Add("fldMonthsActive", typeof(string));
        feePaying.Rows.Add(contractId, matchedParticipantId, "S1", (object?)monthsActive ?? DBNull.Value);

        var nonFeePaying = feePaying.Clone();

        var dataSet = new DataSet();
        dataSet.Tables.Add(addresses);
        dataSet.Tables.Add(feePaying);
        dataSet.Tables.Add(nonFeePaying);
        return dataSet;
    }

    [Fact]
    public async Task GetSampleAddressesAsync_AttachesMatchingSchemesAndTrimsMonthsActive_LeavesUnmatchedEmpty()
    {
        var (repository, connection) = CreateRepository();
        var contractId = Guid.NewGuid();
        var matchedParticipantId = Guid.NewGuid();
        var unmatchedParticipantId = Guid.NewGuid();
        connection.RespondToQuery(GetSampleAddressesSql, SampleAddressDataSet(contractId, matchedParticipantId, unmatchedParticipantId, "Jan, Feb, "));

        var result = await repository.GetSampleAddressesAsync();

        Assert.Equal(2, result.Count);
        var matched = result.Single(a => a.ParticipantId == matchedParticipantId);
        var unmatched = result.Single(a => a.ParticipantId == unmatchedParticipantId);
        var scheme = Assert.Single(matched.FeePayingSchemes);
        Assert.Equal("Jan, Feb", scheme.MonthsActive);
        Assert.Empty(matched.NonFeePayingSchemes);
        Assert.Empty(unmatched.FeePayingSchemes);
        Assert.Empty(unmatched.NonFeePayingSchemes);
    }

    [Fact]
    public async Task GetSampleAddressesAsync_NullMonthsActive_DefaultsToEmptyAfterTrim()
    {
        var (repository, connection) = CreateRepository();
        var contractId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        connection.RespondToQuery(GetSampleAddressesSql, SampleAddressDataSet(contractId, participantId, Guid.NewGuid(), monthsActive: null));

        var result = await repository.GetSampleAddressesAsync();

        var matched = result.Single(a => a.ParticipantId == participantId);
        var scheme = Assert.Single(matched.FeePayingSchemes);
        Assert.Equal(string.Empty, scheme.MonthsActive);
    }

    [Fact]
    public async Task GetSampleAddressesAsync_NoAddresses_ReturnsEmptyList()
    {
        var (repository, connection) = CreateRepository();
        var dataSet = SampleAddressDataSet(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
        foreach (DataTable table in dataSet.Tables)
        {
            table.Rows.Clear();
        }
        connection.RespondToQuery(GetSampleAddressesSql, dataSet);

        var result = await repository.GetSampleAddressesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRenewalsAsync_ReturnsMappedRenewals()
    {
        var (repository, connection) = CreateRepository();
        var contractId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldQALNumber", typeof(string));
        table.Rows.Add(contractId, Guid.NewGuid(), "QAL/00001");
        connection.RespondToQuery(GetRenewalsSql, table);

        var result = await repository.GetRenewalsAsync(nonUk: true);

        var renewal = Assert.Single(result);
        Assert.Equal(contractId, renewal.ContractId);
        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == GetRenewalsSql);
        Assert.Equal(true, command.ParameterValue("@NonUk"));
    }
}
