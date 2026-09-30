using System.Data;
using PTL.Core.Contract.PendingOrder;
using PTL.Data.Contract.PendingOrder;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Contract;

// Exercises PendingOrderRepository against a fake ADO.NET connection (see Fakes/).
public class PendingOrderRepositoryTests
{
    public PendingOrderRepositoryTests() => DapperColumnMappings.Register();

    private const string GetSummariesSql = "EXEC dbo.spgaPendingContracts";
    private const string GetByIdSql = "EXEC dbo.spgPendingContractByPendingContractId @PendingContractID";
    private const string GetSchemesSql = "EXEC dbo.spgPendingParticipantSchemeByPendingContractId @PendingContractId";
    private const string GetSchemeByIdSql = "EXEC dbo.spgPendingParticipantSchemeByPendingParticipantSchemeId @PendingParticipantSchemeId = @PendingParticipantSchemeId";
    private const string DeleteSchemeSql = "EXEC dbo.spdPendingParticipantSchemeByPendingParticipantSchemeId @PendingParticipantSchemeId";
    private const string MarkDecidedSql = "EXEC dbo.spdPendingContractByPendingContractID @CustomerId, @PendingContractId";
    private const string UpdateOrderSql = "EXEC dbo.spuPendingContractByPendingContractID @PendingContractId = @PendingContractId, @YearId = @YearId, @IsSubmitted = @IsSubmitted, @PurchaseOrderNumber = @PurchaseOrderNumber, @IsDeleted = @IsDeleted";
    private const string UpdateSchemeSql = "EXEC dbo.spuPendingParticipantSchemeByPendingParticipantSchemeId @PendingParticipantSchemeId = @PendingParticipantSchemeId, @DistributionMonthJan = @DistributionMonthJan, @DistributionMonthFeb = @DistributionMonthFeb, @DistributionMonthMar = @DistributionMonthMar, @DistributionMonthApr = @DistributionMonthApr, @DistributionMonthMay = @DistributionMonthMay, @DistributionMonthJun = @DistributionMonthJun, @DistributionMonthJul = @DistributionMonthJul, @DistributionMonthAug = @DistributionMonthAug, @DistributionMonthSep = @DistributionMonthSep, @DistributionMonthOct = @DistributionMonthOct, @DistributionMonthNov = @DistributionMonthNov, @DistributionMonthDec = @DistributionMonthDec, @IsRemoved = @IsRemoved, @ImportExportLicenceRequired = @ImportExportLicenceRequired, @IsSelected = @IsSelected, @DataConsentDeclarationGiven = @DataConsentDeclarationGiven";

    private static (PendingOrderRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new PendingOrderRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static DataTable SummaryTable(Guid pendingContractId, Guid customerId, bool isDeleted = false)
    {
        var table = new DataTable();
        table.Columns.Add("fldPendingContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldIsSubmitted", typeof(bool));
        table.Columns.Add("fldIsDeleted", typeof(bool));
        table.Columns.Add("fldOrderSubmitDate", typeof(DateTime));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Rows.Add(pendingContractId, customerId, 2026, true, isDeleted, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Sample Labs", "QAL0001");
        return table;
    }

    private static DataTable OrderTable(Guid pendingContractId, Guid customerId, bool isDeleted = false)
    {
        var table = new DataTable();
        table.Columns.Add("fldPendingContractId", typeof(Guid));
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldIsSubmitted", typeof(bool));
        table.Columns.Add("fldIsDeleted", typeof(bool));
        table.Columns.Add("fldPurchaseOrderNumber", typeof(string));
        table.Columns.Add("fldOrderSubmitDate", typeof(DateTime));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldSymbol", typeof(string));
        table.Rows.Add(pendingContractId, customerId, 2026, true, isDeleted, "PO-1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Sample Labs", "£");
        return table;
    }

    private static DataTable SchemeTable(Guid pendingParticipantSchemeId, Guid pendingContractId)
    {
        var table = new DataTable();
        table.Columns.Add("fldPendingParticipantSchemeId", typeof(Guid));
        table.Columns.Add("fldPendingContractId", typeof(Guid));
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldParticipantName", typeof(string));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldApr", typeof(bool));
        table.Columns.Add("fldImportExportLicenceRequired", typeof(bool));
        table.Columns.Add("fldIsRemoved", typeof(bool));
        table.Columns.Add("fldIsSelected", typeof(bool));
        table.Columns.Add("fldPrice", typeof(decimal));
        table.Rows.Add(pendingParticipantSchemeId, pendingContractId, Guid.NewGuid(), "001: Alpha Lab", Guid.NewGuid(), "Sample Scheme", "S1", true, true, false, true, 100m);
        return table;
    }

    [Fact]
    public async Task GetSummariesAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var pendingContractId = Guid.NewGuid();
        connection.RespondToQuery(GetSummariesSql, SummaryTable(pendingContractId, Guid.NewGuid()));

        var result = await repository.GetSummariesAsync();

        Assert.Single(result);
        Assert.Equal(pendingContractId, result[0].PendingContractId);
        Assert.Equal("QAL0001", result[0].QalNumber);
        Assert.Equal(2026, result[0].YearId);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedOrder()
    {
        var (repository, connection) = CreateRepository();
        var pendingContractId = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, OrderTable(pendingContractId, Guid.NewGuid()));

        var result = await repository.GetByIdAsync(pendingContractId);

        Assert.NotNull(result);
        Assert.Equal("PO-1", result!.PurchaseOrderNumber);
        Assert.Equal("£", result.CurrencySymbol);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByIdSql, new DataTable());

        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetSchemesAsync_ReturnsMappedSchemeLines()
    {
        var (repository, connection) = CreateRepository();
        var pendingContractId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        connection.RespondToQuery(GetSchemesSql, SchemeTable(schemeId, pendingContractId));

        var result = await repository.GetSchemesAsync(pendingContractId);

        Assert.Single(result);
        Assert.Equal(schemeId, result[0].PendingParticipantSchemeId);
        Assert.Equal("S1", result[0].SchemeIdentifier);
        Assert.True(result[0].DistributionMonthApr);
        Assert.Equal(100m, result[0].Price);
    }

    [Fact]
    public async Task GetSchemeByIdAsync_MapsContractedMonthAndEditFlags()
    {
        var (repository, connection) = CreateRepository();
        var pendingParticipantSchemeId = Guid.NewGuid();
        var table = SchemeTable(pendingParticipantSchemeId, Guid.NewGuid());
        table.Columns.Add("fldCanEditApr", typeof(bool));
        table.Columns.Add("fldDistributionMonthAprIsContracted", typeof(bool));
        table.Rows[0]["fldCanEditApr"] = false;
        table.Rows[0]["fldDistributionMonthAprIsContracted"] = true;
        connection.RespondToQuery(GetSchemeByIdSql, table);

        var result = await repository.GetSchemeByIdAsync(pendingParticipantSchemeId);

        Assert.NotNull(result);
        Assert.False(result!.CanEditApr);
        Assert.True(result.IsContractedApr);
    }

    [Fact]
    public async Task DeleteSchemeAsync_ExecutesDeleteProcedure()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(DeleteSchemeSql, -1);

        await repository.DeleteSchemeAsync(Guid.NewGuid());

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == DeleteSchemeSql);
    }

    [Fact]
    public async Task MarkDecidedAsync_OrderNowDeleted_ReturnsTrue()
    {
        var (repository, connection) = CreateRepository();
        var pendingContractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToNonQuery(MarkDecidedSql, -1);
        connection.RespondToQuery(GetByIdSql, OrderTable(pendingContractId, customerId, isDeleted: true));

        Assert.True(await repository.MarkDecidedAsync(customerId, pendingContractId));
    }

    [Fact]
    public async Task MarkDecidedAsync_OrderStillOutstanding_ReturnsFalse()
    {
        var (repository, connection) = CreateRepository();
        var pendingContractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToNonQuery(MarkDecidedSql, -1);
        connection.RespondToQuery(GetByIdSql, OrderTable(pendingContractId, customerId, isDeleted: false));

        Assert.False(await repository.MarkDecidedAsync(customerId, pendingContractId));
    }

    [Fact]
    public async Task UpdatePurchaseOrderNumberAsync_UnknownOrder_DoesNotExecuteUpdate()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByIdSql, new DataTable());

        await repository.UpdatePurchaseOrderNumberAsync(Guid.NewGuid(), "PO-2");

        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText.Contains("spuPendingContractByPendingContractID", StringComparison.Ordinal));
    }

    // spuPendingContractByPendingContractID declares @PurchaseOrderNumber BEFORE @IsDeleted, so a
    // positional EXEC passes the varchar into the bit parameter and SQL Server throws
    // "Error converting data type nvarchar to bit". Binding by name is what prevents that.
    [Fact]
    public async Task UpdatePurchaseOrderNumberAsync_BindsEveryArgumentByName()
    {
        var (repository, connection) = CreateRepository();
        var pendingContractId = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, OrderTable(pendingContractId, Guid.NewGuid()));
        connection.RespondToNonQuery(UpdateOrderSql, -1);

        await repository.UpdatePurchaseOrderNumberAsync(pendingContractId, "PO-2");

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText.Contains("spuPendingContractByPendingContractID", StringComparison.Ordinal));
        Assert.Contains("@PurchaseOrderNumber = @PurchaseOrderNumber", command.CommandText, StringComparison.Ordinal);
        Assert.Contains("@IsDeleted = @IsDeleted", command.CommandText, StringComparison.Ordinal);
        Assert.Equal("PO-2", command.ParameterValue("PurchaseOrderNumber"));
        Assert.Equal(false, command.ParameterValue("IsDeleted"));
        Assert.Equal(true, command.ParameterValue("IsSubmitted"));
        Assert.Equal(2026, command.ParameterValue("YearId"));
    }

    // Same reasoning as above - 17 arguments makes a positional mismatch easy to introduce.
    [Fact]
    public async Task UpdateSchemeAsync_BindsEveryArgumentByName()
    {
        var (repository, connection) = CreateRepository();
        var pendingParticipantSchemeId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSchemeSql, -1);

        await repository.UpdateSchemeAsync(
            new PendingOrderSchemeEntity
            {
                PendingParticipantSchemeId = pendingParticipantSchemeId,
                DistributionMonthApr = true,
                ImportExportLicenceRequired = true,
                IsSelected = true,
                IsRemoved = false
            },
            dataConsentDeclarationGiven: true);

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Contains("@DataConsentDeclarationGiven = @DataConsentDeclarationGiven", command.CommandText, StringComparison.Ordinal);
        Assert.Equal(pendingParticipantSchemeId, command.ParameterValue("PendingParticipantSchemeId"));
        Assert.Equal(true, command.ParameterValue("DistributionMonthApr"));
        Assert.Equal(false, command.ParameterValue("DistributionMonthMay"));
        Assert.Equal(true, command.ParameterValue("ImportExportLicenceRequired"));
        Assert.Equal(false, command.ParameterValue("IsRemoved"));
        Assert.Equal(true, command.ParameterValue("DataConsentDeclarationGiven"));
    }
}
