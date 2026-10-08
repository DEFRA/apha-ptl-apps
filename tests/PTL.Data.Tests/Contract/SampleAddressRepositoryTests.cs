using System.Data;
using PTL.Data.Contract.SampleAddress;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Contract;

public class SampleAddressRepositoryTests
{
    public SampleAddressRepositoryTests() => DapperColumnMappings.Register();

    private const string GetSql = "EXEC dbo.spgExportSampleAddressesByContractId @ContractId";

    private static DataSet SampleAddressDataSet(Guid contractId, Guid participantId)
    {
        var addresses = new DataTable();
        addresses.Columns.Add("fldContractId", typeof(Guid));
        addresses.Columns.Add("fldParticipantId", typeof(Guid));
        addresses.Columns.Add("fldQalNumber", typeof(string));
        addresses.Columns.Add("fldLabCode", typeof(string));
        addresses.Columns.Add("fldContactName", typeof(string));
        addresses.Columns.Add("fldOrganisation", typeof(string));
        addresses.Columns.Add("fldAddress1", typeof(string));
        addresses.Columns.Add("fldAddress2", typeof(string));
        addresses.Columns.Add("fldAddress3", typeof(string));
        addresses.Columns.Add("fldAddress4", typeof(string));
        addresses.Columns.Add("fldAddress5", typeof(string));
        addresses.Columns.Add("fldCountry", typeof(string));
        addresses.Columns.Add("fldTelephone", typeof(string));
        addresses.Columns.Add("fldFax", typeof(string));
        addresses.Columns.Add("fldEmail", typeof(string));
        addresses.Columns.Add("fldVatNumber", typeof(string));
        addresses.Columns.Add("fldAccountNumber", typeof(string));
        addresses.Columns.Add("fldVatRating", typeof(string));
        addresses.Columns.Add("fldPurchaseOrderNumber", typeof(string));
        addresses.Rows.Add(contractId, participantId, "QAL/00001", "LAB1", "Alice Example", "Lab One Ltd",
            "1 Street", "Town", "County", "Country", "Postcode", "United Kingdom",
            "020 1234 5678", "020 1234 5679", "alice@example.com", "GB123456789", "ACC001", "Standard", "PO12345");

        var feePaying = new DataTable();
        feePaying.Columns.Add("fldContractId", typeof(Guid));
        feePaying.Columns.Add("fldParticipantId", typeof(Guid));
        feePaying.Columns.Add("fldParticipantSchemeId", typeof(Guid));
        feePaying.Columns.Add("fldName", typeof(string));
        feePaying.Columns.Add("fldIdentifier", typeof(string));
        feePaying.Columns.Add("fldMonthsActive", typeof(string));
        feePaying.Columns.Add("fldWeekNumber", typeof(int));
        feePaying.Rows.Add(contractId, participantId, Guid.NewGuid(), "Salmonella", "S1", "Jan, Feb, ", 12);

        var nonFeePaying = feePaying.Clone();

        var dataSet = new DataSet();
        dataSet.Tables.Add(addresses);
        dataSet.Tables.Add(feePaying);
        dataSet.Tables.Add(nonFeePaying);
        return dataSet;
    }

    [Fact]
    public async Task GetByContractIdAsync_AttachesFeeAndNonFeePayingSchemesToAddress()
    {
        var connection = new PTL.Data.Tests.Fakes.FakeDbConnection();
        var repository = new SampleAddressRepository(new FakeDbConnectionFactory(connection));
        var contractId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        connection.RespondToQuery(GetSql, SampleAddressDataSet(contractId, participantId));

        var result = await repository.GetByContractIdAsync(contractId);

        var address = Assert.Single(result);
        Assert.Equal("LAB1", address.LabCode);
        var scheme = Assert.Single(address.FeePayingSchemes);
        Assert.Equal("S1", scheme.SchemeIdentifier);
        Assert.Equal("Jan, Feb", scheme.MonthsActive);
        Assert.Empty(address.NonFeePayingSchemes);
    }

    [Fact]
    public async Task GetByContractIdAsync_NoMatchingSchemes_LeavesBothListsEmpty()
    {
        var connection = new PTL.Data.Tests.Fakes.FakeDbConnection();
        var repository = new SampleAddressRepository(new FakeDbConnectionFactory(connection));
        var contractId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var dataSet = SampleAddressDataSet(contractId, participantId);
        dataSet.Tables[1]!.Rows.Clear();
        connection.RespondToQuery(GetSql, dataSet);

        var result = await repository.GetByContractIdAsync(contractId);

        var address = Assert.Single(result);
        Assert.Empty(address.FeePayingSchemes);
        Assert.Empty(address.NonFeePayingSchemes);
    }

    [Fact]
    public async Task GetByContractIdAsync_NullMonthsActive_DefaultsToEmptyAfterTrim()
    {
        var connection = new PTL.Data.Tests.Fakes.FakeDbConnection();
        var repository = new SampleAddressRepository(new FakeDbConnectionFactory(connection));
        var contractId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var dataSet = SampleAddressDataSet(contractId, participantId);
        dataSet.Tables[1]!.Rows[0]["fldMonthsActive"] = DBNull.Value;
        connection.RespondToQuery(GetSql, dataSet);

        var result = await repository.GetByContractIdAsync(contractId);

        var address = Assert.Single(result);
        var scheme = Assert.Single(address.FeePayingSchemes);
        Assert.Equal(string.Empty, scheme.MonthsActive);
    }
}
