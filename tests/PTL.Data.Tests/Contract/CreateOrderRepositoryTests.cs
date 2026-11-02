using System.Data;
using PTL.Core.Contract.PendingOrder;
using PTL.Data.Contract.CreateOrder;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Contract;

// Exercises CreateOrderRepository against a fake ADO.NET connection (see Fakes/).
public class CreateOrderRepositoryTests
{
    public CreateOrderRepositoryTests() => DapperColumnMappings.Register();

    private const string GetOrderSql = "EXEC dbo.spgPendingContractByCustomerID @CustomerId = @CustomerId, @IsSubmitted = @IsSubmitted, @YearId = @YearId";
    private const string CreateOrderSql = "EXEC dbo.spiPendingContract @PendingContractId = @PendingContractId, @CustomerId = @CustomerId, @YearId = @YearId, @IsSubmitted = @IsSubmitted, @PurchaseOrderNumber = @PurchaseOrderNumber";
    private const string AvailableSql = "EXEC dbo.spgaAvailableSchemeInfoByYearId @YearId = @YearId";
    private const string ParticipatingSql = "EXEC dbo.spgPastParticipantSchemeByParticipantSchemeId @participantId = @ParticipantId, @yearId = @YearId, @participatingYearId = @ParticipatingYearId";
    private const string InsertSchemeSql = "EXEC dbo.spiPendingParticipantScheme @PendingParticipantSchemeId = @PendingParticipantSchemeId, @ParticipantId = @ParticipantId, @SchemeId = @SchemeId, @PendingContractId = @PendingContractId, @DistributionMonthJan = @DistributionMonthJan, @DistributionMonthFeb = @DistributionMonthFeb, @DistributionMonthMar = @DistributionMonthMar, @DistributionMonthApr = @DistributionMonthApr, @DistributionMonthMay = @DistributionMonthMay, @DistributionMonthJun = @DistributionMonthJun, @DistributionMonthJul = @DistributionMonthJul, @DistributionMonthAug = @DistributionMonthAug, @DistributionMonthSep = @DistributionMonthSep, @DistributionMonthOct = @DistributionMonthOct, @DistributionMonthNov = @DistributionMonthNov, @DistributionMonthDec = @DistributionMonthDec, @NonUk = @NonUk, @IsRemoved = @IsRemoved, @ImportExportLicenceRequired = @ImportExportLicenceRequired, @IsSelected = @IsSelected, @DataConsentDeclarationGiven = @DataConsentDeclarationGiven";

    private static (CreateOrderRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new CreateOrderRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static DataTable OrderTable(Guid pendingContractId, Guid customerId)
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
        table.Rows.Add(pendingContractId, customerId, 2026, false, false, string.Empty, DBNull.Value, "Sample Labs", "£");
        return table;
    }

    [Fact]
    public async Task GetPendingOrderAsync_Found_ReturnsMappedOrderAndBindsParameters()
    {
        var (repository, connection) = CreateRepository();
        var pendingContractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        connection.RespondToQuery(GetOrderSql, OrderTable(pendingContractId, customerId));

        var result = await repository.GetPendingOrderAsync(customerId, 2026, isSubmitted: false);

        Assert.NotNull(result);
        Assert.Equal(pendingContractId, result!.PendingContractId);
        Assert.Equal("£", result.CurrencySymbol);
        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == GetOrderSql);
        Assert.Equal(customerId, command.ParameterValue("CustomerId"));
        Assert.Equal(false, command.ParameterValue("IsSubmitted"));
        Assert.Equal(2026, command.ParameterValue("YearId"));
    }

    [Fact]
    public async Task GetPendingOrderAsync_NoRows_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetOrderSql, OrderTable(Guid.NewGuid(), Guid.NewGuid()).Clone());

        Assert.Null(await repository.GetPendingOrderAsync(Guid.NewGuid(), 2026, isSubmitted: true));
    }

    [Fact]
    public async Task CreatePendingOrderAsync_InsertsUnsubmittedOrderWithEmptyPurchaseOrderNumber()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(CreateOrderSql, -1);
        var pendingContractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        await repository.CreatePendingOrderAsync(pendingContractId, customerId, 2026);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == CreateOrderSql);
        Assert.Equal(pendingContractId, command.ParameterValue("PendingContractId"));
        Assert.Equal(customerId, command.ParameterValue("CustomerId"));
        Assert.Equal(false, command.ParameterValue("IsSubmitted"));
        Assert.Equal(string.Empty, command.ParameterValue("PurchaseOrderNumber"));
    }

    [Fact]
    public async Task GetAvailableSchemesAsync_MapsSchemeMonthsEditFlagsAndPostage()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var postageId = Guid.NewGuid();
        var dayId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldSchedule", typeof(string));
        table.Columns.Add("fldComerciallyAvailable", typeof(bool));
        table.Columns.Add("fldPostage", typeof(Guid));
        table.Columns.Add("fldCombinedPackaging", typeof(bool));
        table.Columns.Add("fldDayOfWeekId", typeof(Guid));
        table.Columns.Add("fldWeekNumber", typeof(int));
        table.Columns.Add("fldDistributionMonthApr", typeof(bool));
        table.Columns.Add("fldCanEditApr", typeof(bool));
        table.Columns.Add("fldUnmappedColumn", typeof(string));
        table.Rows.Add(schemeId, "PT1", "Alpha", "Monthly", true, postageId, true, dayId, 2, true, false, "ignored");
        connection.RespondToQuery(AvailableSql, table);

        var result = await repository.GetAvailableSchemesAsync(2026);

        var scheme = Assert.Single(result);
        Assert.Equal(schemeId, scheme.SchemeId);
        Assert.Equal("PT1", scheme.Identifier);
        Assert.Equal("Alpha", scheme.Name);
        Assert.Equal("Monthly", scheme.Schedule);
        Assert.True(scheme.ComerciallyAvailable);
        Assert.Equal(postageId, scheme.Postage);
        Assert.True(scheme.CombinedPackaging);
        Assert.Equal(dayId, scheme.DayOfWeekId);
        Assert.Equal(2, scheme.WeekNumber);
        Assert.True(scheme.Offers(4));
        Assert.False(scheme.Offers(5));
        Assert.False(scheme.CanEdit(4));
        Assert.True(scheme.CanEdit(5));
    }

    [Fact]
    public async Task GetParticipatingSchemesAsync_MapsHeldMonthsAndBindsBothYears()
    {
        var (repository, connection) = CreateRepository();
        var participantId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldParticipantId", typeof(Guid));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldSchedule", typeof(string));
        table.Columns.Add("fldDistributionMonthJan", typeof(bool));
        table.Columns.Add("fldDistributionMonthDec", typeof(bool));
        table.Rows.Add(participantId, schemeId, "PT1", "Alpha", "Monthly", true, false);
        connection.RespondToQuery(ParticipatingSql, table);

        var result = await repository.GetParticipatingSchemesAsync(participantId, 10, 9);

        var scheme = Assert.Single(result);
        Assert.Equal(schemeId, scheme.SchemeId);
        Assert.True(scheme.Holds(1));
        Assert.False(scheme.Holds(12));
        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == ParticipatingSql);
        Assert.Equal(participantId, command.ParameterValue("ParticipantId"));
        Assert.Equal(10, command.ParameterValue("YearId"));
        Assert.Equal(9, command.ParameterValue("ParticipatingYearId"));
    }

    [Fact]
    public async Task InsertSchemeAsync_BindsEveryProcedureArgumentByName()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(InsertSchemeSql, -1);
        var line = new PendingOrderSchemeEntity
        {
            PendingParticipantSchemeId = Guid.NewGuid(),
            ParticipantId = Guid.NewGuid(),
            SchemeId = Guid.NewGuid(),
            PendingContractId = Guid.NewGuid(),
            DistributionMonthApr = true,
            ImportExportLicenceRequired = true,
            IsSelected = true
        };

        await repository.InsertSchemeAsync(line, nonUk: true);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSchemeSql);
        Assert.Equal(line.PendingParticipantSchemeId, command.ParameterValue("PendingParticipantSchemeId"));
        Assert.Equal(line.ParticipantId, command.ParameterValue("ParticipantId"));
        Assert.Equal(line.SchemeId, command.ParameterValue("SchemeId"));
        Assert.Equal(line.PendingContractId, command.ParameterValue("PendingContractId"));
        Assert.Equal(true, command.ParameterValue("DistributionMonthApr"));
        Assert.Equal(false, command.ParameterValue("DistributionMonthMay"));
        Assert.Equal(true, command.ParameterValue("NonUk"));
        Assert.Equal(true, command.ParameterValue("ImportExportLicenceRequired"));
        Assert.Equal(true, command.ParameterValue("IsSelected"));
        Assert.Equal(false, command.ParameterValue("IsRemoved"));
        Assert.Equal(false, command.ParameterValue("DataConsentDeclarationGiven"));
    }
}
