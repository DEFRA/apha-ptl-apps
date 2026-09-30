using System.Data;
using PTL.Data.Contract.Renew;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Contract;

public class ContractMergeRepositoryTests
{
    public ContractMergeRepositoryTests() => DapperColumnMappings.Register();

    private const string GetSql = "EXEC dbo.spgContractMerge @CustomerId";

    private static DataSet MergeDataSet(Guid contractId, string suffix, Guid participantSchemeId)
    {
        var contracts = new DataTable();
        contracts.Columns.Add("fldContractId", typeof(Guid));
        contracts.Columns.Add("fldSuffix", typeof(string));
        contracts.Columns.Add("fldContractSignatory", typeof(string));
        contracts.Columns.Add("fldRenewalInformation", typeof(string));
        contracts.Columns.Add("fldActionsRequired", typeof(string));
        contracts.Columns.Add("fldIsActive", typeof(bool));
        contracts.Columns.Add("fldNoOfItems", typeof(int));
        contracts.Rows.Add(contractId, suffix, "Alice Example", "Renewal info", string.Empty, true, 1);

        var items = new DataTable();
        items.Columns.Add("fldContractId", typeof(Guid));
        items.Columns.Add("fldParticipantSchemeId", typeof(Guid));
        items.Columns.Add("fldLabCode", typeof(string));
        items.Columns.Add("fldLabName", typeof(string));
        items.Columns.Add("fldOldSchemeIdentifier", typeof(string));
        items.Columns.Add("fldOldSchemeName", typeof(string));
        items.Columns.Add("fldNewSchemeIdentifier", typeof(string));
        items.Columns.Add("fldNewSchemeName", typeof(string));
        items.Rows.Add(contractId, participantSchemeId, "LAB1", "Lab One", "S1", "Old Scheme", "S2", "New Scheme");

        var dataSet = new DataSet();
        dataSet.Tables.Add(contracts);
        dataSet.Tables.Add(items);
        return dataSet;
    }

    [Fact]
    public async Task GetContractMergeInfoAsync_ReturnsContractsAndItemsWithSuffixAttached()
    {
        var connection = new PTL.Data.Tests.Fakes.FakeDbConnection();
        var repository = new ContractMergeRepository(new FakeDbConnectionFactory(connection));
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        connection.RespondToQuery(GetSql, MergeDataSet(contractId, "A", participantSchemeId));

        var result = await repository.GetContractMergeInfoAsync(Guid.NewGuid());

        var contract = Assert.Single(result.Contracts);
        Assert.Equal(contractId, contract.ContractId);
        Assert.Equal("A", contract.Suffix);
        var item = Assert.Single(result.ParticipantSchemes);
        Assert.Equal(participantSchemeId, item.ParticipantSchemeId);
        Assert.Equal("A", item.Suffix);
        Assert.True(item.IsRenewable);
        Assert.Equal("LAB1S1", item.Identifier);
    }
}
