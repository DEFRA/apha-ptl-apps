using System.Data;
using PTL.Data.Infrastructure;
using PTL.Data.Invoice;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Invoice;

// Exercises InvoiceRepository against a fake ADO.NET connection (see Fakes/) instead of a live SQL
// Server - see SchemeRepositoryTests for the pattern this follows.
public class InvoiceRepositoryTests
{
    public InvoiceRepositoryTests() => DapperColumnMappings.Register();

    private const string GetPendingDataSql = "EXEC dbo.spgaExportContractDetailsForAutomaticInvoicing";
    private const string MarkInvoicedSql = "EXEC dbo.sppUpdateInvoiceItems";
    private const string AuditSql = "EXEC dbo.spiAuditInvoiceGeneration @AuditWho";
    private const string ResetSql = "EXEC dbo.sppResetInvoiceItems";
    private const string AuditHistorySql =
        """
        SELECT fldAuditInvoiceGenerationId AS AuditInvoiceGenerationId, fldAuditWho AS AuditWho, fldAuditDate AS AuditDate
        FROM tblAuditInvoiceGeneration
        ORDER BY fldAuditDate DESC
        """;

    private static (InvoiceRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new InvoiceRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    private static DataTable ContractsTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldSuffix", typeof(string));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Columns.Add("fldPurchaseOrderNumber", typeof(string));
        table.Columns.Add("fldAdministrationCharge", typeof(decimal));
        table.Columns.Add("fldDiscountRate", typeof(decimal));
        table.Columns.Add("fldNumberPostage", typeof(int));
        table.Columns.Add("fldPostagePrice", typeof(decimal));
        table.Columns.Add("fldNumberCourier", typeof(int));
        table.Columns.Add("fldCourierPrice", typeof(decimal));
        table.Columns.Add("fldNumberSpecialDelivery", typeof(int));
        table.Columns.Add("fldSpecialDeliveryPrice", typeof(decimal));
        table.Columns.Add("fldInvoiceOrganisation", typeof(string));
        table.Columns.Add("fldInvoiceAddress1", typeof(string));
        table.Columns.Add("fldInvoiceAddress2", typeof(string));
        table.Columns.Add("fldInvoiceAddress3", typeof(string));
        table.Columns.Add("fldInvoiceAddress4", typeof(string));
        table.Columns.Add("fldInvoiceAddress5", typeof(string));
        table.Columns.Add("fldInvoiceCountry", typeof(string));
        table.Columns.Add("fldVatNumber", typeof(string));
        table.Columns.Add("fldVatRating", typeof(string));
        table.Columns.Add("fldCustomerNumber", typeof(string));
        table.Columns.Add("fldCustomerType", typeof(string));
        table.Columns.Add("fldOptOutOfInvoiceGeneration", typeof(bool));
        return table;
    }

    private static DataTable ItemsTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldParticipantSchemeId", typeof(Guid));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldPrice", typeof(decimal));
        table.Columns.Add("fldNonFeePaying", typeof(bool));
        table.Columns.Add("fldHasOverride", typeof(bool));
        return table;
    }

    [Fact]
    public async Task GetPendingInvoiceDataAsync_ReturnsContractsAndItems()
    {
        var (repository, connection) = CreateRepository();
        var contractId = Guid.NewGuid();

        var contracts = ContractsTable();
        contracts.Rows.Add(
            contractId, Guid.NewGuid(), "A", 2026, "QAL001", "PO1",
            0m, 0m, 0, 0m, 0, 0m, 0, 0m,
            "Org", "Line1", "", "", "", "", "UK",
            "VAT1", "Standard", "CUST1", "Business", false);

        var items = ItemsTable();
        items.Rows.Add(contractId, Guid.NewGuid(), "PT0001", "Scheme One", 100m, false, false);

        var dataSet = new DataSet();
        dataSet.Tables.Add(contracts);
        dataSet.Tables.Add(items);
        connection.RespondToQuery(GetPendingDataSql, dataSet);

        var result = await repository.GetPendingInvoiceDataAsync();

        Assert.Single(result.Contracts);
        Assert.Single(result.Items);
        Assert.Equal(contractId, result.Contracts[0].ContractId);
        Assert.Equal(contractId, result.Items[0].ContractId);
    }

    [Fact]
    public async Task MarkInvoicedAndRecordAuditAsync_ExecutesUpdateAndAuditWithinCommittedTransaction()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(MarkInvoicedSql, 5);
        connection.RespondToNonQuery(AuditSql, 1);

        await repository.MarkInvoicedAndRecordAuditAsync("test.user");

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.Committed);
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == MarkInvoicedSql);
        var auditCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSql);
        Assert.Equal("test.user", auditCommand.ParameterValue("@AuditWho"));
    }

    [Fact]
    public async Task ResetInvoicedFlagsAsync_ExecutesResetProcedure()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(ResetSql, 3);

        await repository.ResetInvoicedFlagsAsync();

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == ResetSql);
    }

    [Fact]
    public async Task GetAuditHistoryAsync_ReturnsMappedRecordsInDescendingDateOrder()
    {
        var (repository, connection) = CreateRepository();
        var auditId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("AuditInvoiceGenerationId", typeof(Guid));
        table.Columns.Add("AuditWho", typeof(string));
        table.Columns.Add("AuditDate", typeof(DateTime));
        table.Rows.Add(auditId, "test.user", new DateTime(2026, 1, 15));
        connection.RespondToQuery(AuditHistorySql, table);

        var result = await repository.GetAuditHistoryAsync();

        Assert.Single(result);
        Assert.Equal(auditId, result[0].AuditInvoiceGenerationId);
        Assert.Equal("test.user", result[0].AuditWho);
    }
}
