using System.Data;
using PTL.Contracts.Contract;
using PTL.Core.Contract;
using PTL.Data.Contract;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using CoreContract = PTL.Core.Contract.Contract;

namespace PTL.Data.Tests.Contract;

// Exercises ContractRepository against a fake ADO.NET connection (see Fakes/) instead of a live
// SQL Server - proves the repository builds the right "EXEC dbo.spXxx" command text/parameters
// and correctly materialises Dapper's result via DapperColumnMappings, without needing a database.
public class ContractRepositoryTests
{
    public ContractRepositoryTests() => DapperColumnMappings.Register();

    private const string GetByIdSql = "EXEC dbo.spgContractByContractId @ContractId";
    private const string InsertSql =
        "EXEC dbo.spiContract @ContractId, @CustomerId, @YearId, @UTNumber, @FTNumber, @ContractSignatory, @ActionsRequired, @RenewalInformation, @DiscountRate, @AdministrationCharge, @NumberCourier, @CourierPrice, @NumberPostage, @PostagePrice, @NumberSpecialDelivery, @SpecialDeliveryPrice, @AcknowledgementPostedDate, @AcknowledgementReturnedDate, @JobSheetPostedDate, @ReasonForClosure, @DateOfLeaving, @IsActive, @Suffix, @CommencementDate, @PurchaseOrderNumber, @OptOutOfInvoiceGeneration, @IsOnlineOrder, @ApprovedBy, @ApprovedDate";
    private const string UpdateSql =
        "EXEC dbo.spuContract @ContractId, @CustomerId, @YearId, @UTNumber, @FTNumber, @ContractSignatory, @ActionsRequired, @RenewalInformation, @DiscountRate, @AdministrationCharge, @NumberCourier, @CourierPrice, @NumberPostage, @PostagePrice, @NumberSpecialDelivery, @SpecialDeliveryPrice, @AcknowledgementPostedDate, @AcknowledgementReturnedDate, @JobSheetPostedDate, @ReasonForClosure, @DateOfLeaving, @IsActive, @Suffix, @CommencementDate, @PurchaseOrderNumber, @OptOutOfInvoiceGeneration, @IsOnlineOrder, @ApprovedBy, @ApprovedDate";

    private static DataTable ContractTable(Guid contractId, Guid customerId, int yearId = 2026, bool isActive = true)
    {
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldCustomerName", typeof(string));
        table.Columns.Add("fldQALNumber", typeof(string));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Columns.Add("Readonly", typeof(bool));
        table.Rows.Add(contractId, customerId, "Test Customer", "QAL0001", yearId, isActive, false);
        return table;
    }

    private static (ContractRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new ContractRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedContract()
    {
        var (repository, connection) = CreateRepository();
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, ContractTable(contractId, customerId));

        var result = await repository.GetByIdAsync(contractId);

        Assert.NotNull(result);
        Assert.Equal(contractId, result!.ContractId);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal("Test Customer", result.CustomerName);
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
    public async Task GetSummariesAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var customerId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Columns.Add("fldSuffix", typeof(string));
        table.Rows.Add(Guid.NewGuid(), customerId, 2026, true, "A");
        connection.RespondToQuery("EXEC dbo.spgContractInfoByCustomerId @CustomerId, @Active", table);

        var result = await repository.GetSummariesAsync(customerId, ContractPeriodFilter.CurrentAndNext);

        Assert.Single(result);
        Assert.Equal(customerId, result[0].CustomerId);
    }

    [Fact]
    public async Task GetSummariesByYearAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var customerId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Columns.Add("fldSuffix", typeof(string));
        table.Rows.Add(Guid.NewGuid(), customerId, 2026, true, "A");
        connection.RespondToQuery("EXEC dbo.spgContractInfoByCustomerIdAndYearId @CustomerId, @YearId", table);

        var result = await repository.GetSummariesByYearAsync(customerId, 2026);

        Assert.Single(result);
        Assert.Equal(2026, result[0].YearId);
    }

    [Fact]
    public async Task CreateAsync_InsertsAndReReadsContract()
    {
        var (repository, connection) = CreateRepository();
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(GetByIdSql, ContractTable(contractId, customerId));
        var contract = new CoreContract { ContractId = contractId, CustomerId = customerId, YearId = 2026, IsActive = true };

        var result = await repository.CreateAsync(contract);

        Assert.Equal(contractId, result.ContractId);
        var insertCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSql);
        Assert.Equal(contractId, insertCommand.ParameterValue("@ContractId"));
    }

    [Fact]
    public async Task UpdateAsync_ExistingContract_UpdatesAndReReads()
    {
        var (repository, connection) = CreateRepository();
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToQuery(GetByIdSql, ContractTable(contractId, customerId));
        var contract = new CoreContract { ContractId = contractId, CustomerId = customerId, YearId = 2026, IsActive = true };

        var result = await repository.UpdateAsync(contract);

        Assert.NotNull(result);
        Assert.Equal(contractId, result!.ContractId);
    }

    [Fact]
    public async Task UpdateAsync_UnknownContract_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSql, 0);
        var contract = new CoreContract { ContractId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), YearId = 2026 };

        var result = await repository.UpdateAsync(contract);

        Assert.Null(result);
    }
}
