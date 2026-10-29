using System.Data;
using PTL.Core.Scheme;
using PTL.Data.Infrastructure;
using PTL.Data.Scheme;
using PTL.Data.Tests.Fakes;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Data.Tests.Scheme;

// Exercises SchemeRepository against a fake ADO.NET connection (see Fakes/) instead of a live SQL
// Server - see ContractRepositoryTests for the pattern this follows.
public class SchemeRepositoryTests
{
    public SchemeRepositoryTests() => DapperColumnMappings.Register();

    private const string GetByIdSql = "EXEC dbo.spgSchemeBySchemeId @SchemeId";
    private const string GetSummariesSql = "EXEC dbo.spgSchemeInfoByYearId @YearId";
    private const string GetSummariesBySchemeIdSql = "EXEC dbo.spgSchemeInfoBySchemeId @SchemeId";
    private const string GetHistorySql = "EXEC dbo.spgSchemeInfoBySharedId @SharedId";
    private const string InsertSql =
        "EXEC dbo.spiScheme @SchemeId, @SharedId, @YearId, @Identifier, @Name, @ScheduleId, @ScheduleCodeId, @StartDate, @DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, @DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, @DistributionAsAvailable, @WeekNumber, @DayOfWeekId, @Deadline, @Pilot, @Accredited, @ComerciallyAvailable, @LimitedSampleAvailability, @NoVLALabs, @CombinedPackaging, @SampleOrigin, @Subcontractor, @NumberOfSamples, @SamplePackingInstructions, @TestConsultant1, @TestConsultant2, @TestConsultant3, @CommentsRequired, @DateOfReceipt, @StorageConditions, @ConditionOnReceipt, @Instructions, @TestConsultantTabulationId, @UseExternalReference, @LastModified, @StoreRatings, @Assessor1, @Assessor2, @Assessor3, @Assessor4, @RequiresAssessment, @StandardTabulationText, @Postage, @CustomsDocumentDescription, @CustomsDocumentVolume, @DataConsentDeclarationActive, @DataConsentDeclarationText";
    private const string UpdateSql =
        "EXEC dbo.spuScheme @SchemeId, @SharedId, @YearId, @Identifier, @Name, @ScheduleId, @ScheduleCodeId, @StartDate, @DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, @DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, @DistributionAsAvailable, @WeekNumber, @DayOfWeekId, @Deadline, @Pilot, @Accredited, @ComerciallyAvailable, @LimitedSampleAvailability, @NoVLALabs, @CombinedPackaging, @SampleOrigin, @Subcontractor, @NumberOfSamples, @SamplePackingInstructions, @TestConsultant1, @TestConsultant2, @TestConsultant3, @CommentsRequired, @DateOfReceipt, @StorageConditions, @ConditionOnReceipt, @Instructions, @TestConsultantTabulationId, @UseExternalReference, @LastModified, @StoreRatings, @Assessor1, @Assessor2, @Assessor3, @Assessor4, @RequiresAssessment, @StandardTabulationText, @Postage, @CustomsDocumentDescription, @CustomsDocumentVolume, @DataConsentDeclarationActive, @DataConsentDeclarationText";

    private const string InsertPriceSql =
        "EXEC dbo.spiSchemeCurrency @SchemeCurrencyId = @SchemeCurrencyId, @SchemeId = @SchemeId, @CurrencyId = @CurrencyId, @Price = @Price";
    private const string UpdatePriceSql =
        "EXEC dbo.spuSchemeCurrency @SchemeCurrencyId = @SchemeCurrencyId, @SchemeId = @SchemeId, @CurrencyId = @CurrencyId, @Price = @Price";

    private const string InsertTestSql =
        "EXEC dbo.spiTest @TestId = @TestId, @TestTypeId = @TestTypeId, @SchemeId = @SchemeId, @Order = @Order";
    private const string InsertResultItemSql =
        "EXEC dbo.spiTestResultItem @TestResultItemId = @TestResultItemId, @TestResultItemTypeId = @TestResultItemTypeId, @TestId = @TestId, @Order = @Order";
    private const string InsertTabulationSql =
        "EXEC dbo.spiTabulation @TabulationId = @TabulationId, @SchemeId = @SchemeId, @Name = @Name, @IntendedResultsOnly = @IntendedResultsOnly, @SingleParticipantTabulation = @SingleParticipantTabulation, @ShowRatings = @ShowRatings, @AvailableToParticipants = @AvailableToParticipants, @AvailableToViewers = @AvailableToViewers";
    private const string InsertTabulationResultItemSql =
        "EXEC dbo.spiTabulationTestResultItem @TabulationTestResultItemId = @LinkId, @TabulationId = @TabulationId, @TestResultItemId = @ItemId";
    private const string InsertTabulationMethodItemSql =
        "EXEC dbo.spiTabulationTestMethodItem @TabulationTestMethodItemId = @LinkId, @TabulationId = @TabulationId, @TestMethodItemId = @ItemId";
    private const string DeleteTabulationResultItemSql = "EXEC dbo.spdTabulationTestResultItem @TabulationTestResultItemId = @LinkId";
    private const string DeleteTabulationMethodItemSql = "EXEC dbo.spdTabulationTestMethodItem @TabulationTestMethodItemId = @LinkId";

    private const string AuditTestSql =
        "EXEC dbo.spiAuditTest @TestId = @Id, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @TestTypeId = @TypeId, @TestTypeIdUpdate = @TypeChanged, @Order = @Order, @OrderUpdate = @OrderChanged, @IsDeleted = @IsDeleted";
    private const string AuditMethodItemSql =
        "EXEC dbo.spiAuditTestMethodItem @TestMethodItemId = @Id, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @TestMethodItemTypeId = @TypeId, @TestMethodItemTypeIdUpdate = @TypeChanged, @Order = @Order, @OrderUpdate = @OrderChanged, @IsDeleted = @IsDeleted";
    private const string AuditResultItemSql =
        "EXEC dbo.spiAuditTestResultItem @TestResultItemId = @Id, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @TestResultItemTypeId = @TypeId, @TestResultItemTypeIdUpdate = @TypeChanged, @Order = @Order, @OrderUpdate = @OrderChanged, @IsDeleted = @IsDeleted";
    private const string AuditCategoryItemSql =
        "EXEC dbo.spiAuditCategoryItem @CategoryItemId = @Id, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @Order = @Order, @OrderUpdate = @OrderChanged, @CategoryItemTypeId = @TypeId, @IsDeleted = @IsDeleted";
    private const string AuditCriterionItemSql =
        "EXEC dbo.spiAuditCriterionItem @CriterionItemId = @Id, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @Order = @Order, @OrderUpdate = @OrderChanged, @CriterionItemTypeId = @TypeId, @IsDeleted = @IsDeleted";
    private const string AuditTabulationSql =
        "EXEC dbo.spiAuditTabulation @TabulationId = @Id, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @Name = @Name, @NameUpdate = @NameChanged, @IntendedResultsOnly = @IntendedResultsOnly, @IntendedResultsOnlyUpdate = @IntendedResultsOnlyChanged, @IsDeleted = @IsDeleted";
    private const string AuditMethodLinkSql =
        "EXEC dbo.spiAuditTabulationTestMethodItem @TabulationTestMethodItemId = @LinkId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @TabulationId = @TabulationId, @TabulationIdUpdate = 0, @TestMethodItemId = @ItemId, @TestMethodItemIdUpdate = 0, @IsDeleted = @IsDeleted";
    private const string AuditResultLinkSql =
        "EXEC dbo.spiAuditTabulationTestResultItem @TabulationTestResultItemId = @LinkId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @TabulationId = @TabulationId, @TabulationIdUpdate = 0, @TestResultItemId = @ItemId, @TestResultItemIdUpdate = 0, @IsDeleted = @IsDeleted";

    private const string AuditWho = "jsmith";
    private const string AuditWhy = "Reordered the items after review";

    private static DataTable SchemeTable(Guid schemeId, int yearId = 2026)
    {
        var table = new DataTable();
        table.Columns.Add("Readonly", typeof(bool));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldName", typeof(string));
        table.Rows.Add(false, schemeId, yearId, "Test Scheme");
        return table;
    }

    private static (SchemeRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new SchemeRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedScheme()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, SchemeTable(schemeId));

        var result = await repository.GetByIdAsync(schemeId);

        Assert.NotNull(result);
        Assert.Equal(schemeId, result!.SchemeId);
        Assert.Equal("Test Scheme", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();

        // A real spgSchemeBySchemeId call still returns the full column set when no scheme
        // matches, so the empty case is modelled as a shaped table with no rows.
        var empty = SchemeTable(Guid.Empty);
        empty.Rows.Clear();
        connection.RespondToQuery(GetByIdSql, empty);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithTestsAndTabulations_AssemblesTheFullTreeInOrder()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var resultItemId = Guid.NewGuid();
        var methodItemId = Guid.NewGuid();
        var criterionId = Guid.NewGuid();
        var tabulationId = Guid.NewGuid();

        var priceTable = new DataTable();
        priceTable.Columns.Add("fldSchemeCurrencyId", typeof(Guid));
        priceTable.Columns.Add("fldCurrencyId", typeof(Guid));
        priceTable.Columns.Add("fldPrice", typeof(decimal));
        priceTable.Columns.Add("fldCurrencyName", typeof(string));
        priceTable.Columns.Add("fldCurrencySymbol", typeof(string));
        priceTable.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), 12.5m, "British Pound", "£");

        // Two tests out of order (fldOrder 2 then 1) so ReadTestsAsync's final OrderBy is genuinely exercised.
        var testTable = new DataTable();
        testTable.Columns.Add("fldTestId", typeof(Guid));
        testTable.Columns.Add("fldTestTypeId", typeof(Guid));
        testTable.Columns.Add("fldTestType", typeof(string));
        testTable.Columns.Add("fldSchemeId", typeof(Guid));
        testTable.Columns.Add("fldOrder", typeof(int));
        var secondTestId = Guid.NewGuid();
        testTable.Rows.Add(secondTestId, Guid.NewGuid(), "Bacteriology", schemeId, 2);
        testTable.Rows.Add(testId, Guid.NewGuid(), "Serology", schemeId, 1);

        var methodItemTable = new DataTable();
        methodItemTable.Columns.Add("fldTestMethodItemId", typeof(Guid));
        methodItemTable.Columns.Add("fldTestMethodItemTypeId", typeof(Guid));
        methodItemTable.Columns.Add("fldTestMethodItemType", typeof(string));
        methodItemTable.Columns.Add("fldTestId", typeof(Guid));
        methodItemTable.Columns.Add("fldOrder", typeof(int));
        methodItemTable.Columns.Add("fldExpectedLength", typeof(int));
        methodItemTable.Rows.Add(methodItemId, Guid.NewGuid(), "ELISA", testId, 1, 0);

        var resultItemTable = new DataTable();
        resultItemTable.Columns.Add("fldTestResultItemId", typeof(Guid));
        resultItemTable.Columns.Add("fldTestResultItemTypeId", typeof(Guid));
        resultItemTable.Columns.Add("fldTestResultItemType", typeof(string));
        resultItemTable.Columns.Add("fldTestId", typeof(Guid));
        resultItemTable.Columns.Add("fldOrder", typeof(int));
        resultItemTable.Columns.Add("fldExpectedLength", typeof(int));
        resultItemTable.Rows.Add(resultItemId, Guid.NewGuid(), "Titre", testId, 1, 3);

        var categoryTable = new DataTable();
        categoryTable.Columns.Add("fldCategoryItemId", typeof(Guid));
        categoryTable.Columns.Add("fldCategoryItemTypeId", typeof(Guid));
        categoryTable.Columns.Add("fldName", typeof(string));
        categoryTable.Columns.Add("fldTestId", typeof(Guid));
        categoryTable.Columns.Add("fldOrder", typeof(int));
        categoryTable.Rows.Add(categoryId, Guid.NewGuid(), "Accuracy", testId, 1);

        var criterionTable = new DataTable();
        criterionTable.Columns.Add("fldCriterionItemId", typeof(Guid));
        criterionTable.Columns.Add("fldCriterionItemTypeId", typeof(Guid));
        criterionTable.Columns.Add("fldName", typeof(string));
        criterionTable.Columns.Add("fldCategoryItemId", typeof(Guid));
        criterionTable.Columns.Add("fldOrder", typeof(int));
        criterionTable.Rows.Add(criterionId, Guid.NewGuid(), "Within range", categoryId, 1);

        var tabulationTable = new DataTable();
        tabulationTable.Columns.Add("fldTabulationId", typeof(Guid));
        tabulationTable.Columns.Add("fldSchemeId", typeof(Guid));
        tabulationTable.Columns.Add("fldName", typeof(string));
        tabulationTable.Columns.Add("fldIntendedResultsOnly", typeof(bool));
        tabulationTable.Columns.Add("fldSingleParticipantTabulation", typeof(bool));
        tabulationTable.Columns.Add("fldShowRatings", typeof(bool));
        tabulationTable.Columns.Add("fldAvailableToParticipants", typeof(bool));
        tabulationTable.Columns.Add("fldAvailableToViewers", typeof(bool));
        tabulationTable.Rows.Add(tabulationId, schemeId, "Published", false, false, false, true, true);

        var tabulationMethodLinkTable = new DataTable();
        tabulationMethodLinkTable.Columns.Add("fldTabulationTestMethodItemId", typeof(Guid));
        tabulationMethodLinkTable.Columns.Add("fldTabulationId", typeof(Guid));
        tabulationMethodLinkTable.Columns.Add("fldTestMethodItemId", typeof(Guid));
        tabulationMethodLinkTable.Rows.Add(Guid.NewGuid(), tabulationId, methodItemId);

        var tabulationResultLinkTable = new DataTable();
        tabulationResultLinkTable.Columns.Add("fldTabulationTestResultItemId", typeof(Guid));
        tabulationResultLinkTable.Columns.Add("fldTabulationId", typeof(Guid));
        tabulationResultLinkTable.Columns.Add("fldTestResultItemId", typeof(Guid));
        tabulationResultLinkTable.Rows.Add(Guid.NewGuid(), tabulationId, resultItemId);

        var dataSet = new DataSet();
        dataSet.Tables.Add(SchemeTable(schemeId));
        dataSet.Tables.Add(priceTable);
        dataSet.Tables.Add(testTable);
        dataSet.Tables.Add(methodItemTable);
        dataSet.Tables.Add(resultItemTable);
        dataSet.Tables.Add(categoryTable);
        dataSet.Tables.Add(criterionTable);
        dataSet.Tables.Add(tabulationTable);
        dataSet.Tables.Add(tabulationMethodLinkTable);
        dataSet.Tables.Add(tabulationResultLinkTable);
        connection.RespondToQuery(GetByIdSql, dataSet);

        var result = await repository.GetByIdAsync(schemeId);

        Assert.NotNull(result);
        Assert.Equal(12.5m, Assert.Single(result!.Prices).Price);

        Assert.Equal(2, result.Tests.Count);
        Assert.Equal("Serology", result.Tests[0].TestType);
        Assert.Equal("Bacteriology", result.Tests[1].TestType);

        var serology = result.Tests[0];
        Assert.Equal("Titre", Assert.Single(serology.ResultItems).TestResultItemType);
        Assert.Equal("ELISA", Assert.Single(serology.MethodItems).TestMethodItemType);
        var category = Assert.Single(serology.Categories);
        Assert.Equal("Accuracy", category.Name);
        Assert.Equal("Within range", Assert.Single(category.Criteria).Name);

        var tabulation = Assert.Single(result.Tabulations);
        Assert.Equal("Published", tabulation.Name);
        Assert.Equal(resultItemId, Assert.Single(tabulation.ResultItemIds));
        Assert.Equal(methodItemId, Assert.Single(tabulation.MethodItemIds));
    }

    [Fact]
    public async Task GetSummariesBySchemeIdAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var sharedId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldSharedId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldNextSchemeId", typeof(Guid));
        table.Columns.Add("fldNextIdentifier", typeof(string));
        table.Columns.Add("fldNextName", typeof(string));
        table.Rows.Add(sharedId, 2026, Guid.NewGuid(), "PT0002", "Next Scheme");
        connection.RespondToQuery(GetSummariesBySchemeIdSql, table);

        var result = await repository.GetSummariesBySchemeIdAsync(Guid.NewGuid());

        Assert.Single(result);
        Assert.Equal(sharedId, result[0].SharedId);
        Assert.Equal("Next Scheme", result[0].NextName);
    }

    [Fact]
    public async Task GetSummariesByYearAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldSharedId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldCurrentSchemeId", typeof(Guid));
        table.Columns.Add("fldCurrentIdentifier", typeof(string));
        table.Columns.Add("fldCurrentName", typeof(string));
        table.Rows.Add(Guid.NewGuid(), 2026, Guid.NewGuid(), "PT0001", "Test Scheme");
        connection.RespondToQuery(GetSummariesSql, table);

        var result = await repository.GetSummariesByYearAsync(2026);

        Assert.Single(result);
        Assert.Equal(2026, result[0].YearId);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsMappedHistory()
    {
        var (repository, connection) = CreateRepository();
        var sharedId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldCurrentSchemeId", typeof(Guid));
        table.Columns.Add("fldSharedId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldCurrentIdentifier", typeof(string));
        table.Columns.Add("fldCurrentName", typeof(string));
        table.Rows.Add(Guid.NewGuid(), sharedId, 2026, "PT0001", "Test Scheme");
        connection.RespondToQuery(GetHistorySql, table);

        var result = await repository.GetHistoryAsync(sharedId);

        Assert.Single(result);
        Assert.Equal(sharedId, result[0].SharedId);
    }

    [Fact]
    public async Task GetCommercialSchemesBySsoIdAsync_ReturnsMappedParticipantSchemes()
    {
        var (repository, connection) = CreateRepository();
        var ssoId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldSchemeCode", typeof(string));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldYear", typeof(string));
        table.Columns.Add("fldIsParticipating", typeof(int));
        table.Rows.Add(schemeId, "PT1234", "Commercial Scheme", "2025/26", 1);
        connection.RespondToQuery("EXEC dbo.spgCommercialSchemesBySsoId @SsoId, @CurrentFinancialYear", table);

        var result = await repository.GetCommercialSchemesBySsoIdAsync(ssoId, 2025);

        Assert.Single(result);
        Assert.Equal(schemeId, result[0].SchemeId);
        Assert.Equal("PT1234", result[0].Identifier);
        Assert.Equal("2025/26", result[0].Year);
        Assert.True(result[0].IsParticipating);
    }

    [Fact]
    public async Task GetNonCommercialSchemesBySsoIdAsync_ReturnsMappedParticipantSchemes()
    {
        var (repository, connection) = CreateRepository();
        var ssoId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldSchemeCode", typeof(string));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldYear", typeof(string));
        table.Columns.Add("fldIsParticipating", typeof(int));
        table.Rows.Add(schemeId, "PT5678", "Non-Commercial Scheme", "2025/26", 1);
        connection.RespondToQuery("EXEC dbo.spgNonCommercialSchemesBySsoId @SsoId, @CurrentFinancialYear", table);

        var result = await repository.GetNonCommercialSchemesBySsoIdAsync(ssoId, 2025);

        Assert.Single(result);
        Assert.Equal(schemeId, result[0].SchemeId);
        Assert.Equal("Non-Commercial Scheme", result[0].Name);
    }

    [Fact]
    public async Task CreateAsync_InsertsAndReReadsScheme()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(GetByIdSql, SchemeTable(schemeId));
        var scheme = new CoreScheme { SchemeId = schemeId, YearId = 2026, Name = "Test Scheme" };

        var result = await repository.CreateAsync(scheme);

        Assert.Equal(schemeId, result.SchemeId);
        var insertCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSql);
        Assert.Equal(schemeId, insertCommand.ParameterValue("@SchemeId"));
    }

    [Fact]
    public async Task CreateAsync_InsertedButNotReReadable_Throws()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(GetByIdSql, new DataTable());
        var scheme = new CoreScheme { SchemeId = Guid.NewGuid(), YearId = 2026, Name = "Test Scheme" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(scheme));
    }

    [Fact]
    public async Task UpdateAsync_ExistingScheme_UpdatesAndReReads()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToQuery(GetByIdSql, SchemeTable(schemeId));
        var scheme = new CoreScheme { SchemeId = schemeId, YearId = 2026, Name = "Test Scheme" };

        var result = await repository.UpdateAsync(scheme);

        Assert.NotNull(result);
        Assert.Equal(schemeId, result!.SchemeId);
    }

    [Fact]
    public async Task UpdateAsync_UnknownScheme_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var unknown = SchemeTable(Guid.Empty);
        unknown.Rows.Clear();
        connection.RespondToQuery(GetByIdSql, unknown);
        connection.RespondToNonQuery(UpdateSql, 0);
        var scheme = new CoreScheme { SchemeId = Guid.NewGuid(), YearId = 2026, Name = "Test Scheme" };

        var result = await repository.UpdateAsync(scheme);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReadsTheCurrencyPricingResultSet()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();

        var prices = new DataTable();
        prices.Columns.Add("fldSchemeCurrencyId", typeof(Guid));
        prices.Columns.Add("fldCurrencyId", typeof(Guid));
        prices.Columns.Add("fldPrice", typeof(decimal));
        prices.Columns.Add("fldCurrencyName", typeof(string));
        prices.Columns.Add("fldCurrencySymbol", typeof(string));
        prices.Rows.Add(Guid.NewGuid(), currencyId, 123.45m, "British Pound", "£");

        var dataSet = new DataSet();
        dataSet.Tables.Add(SchemeTable(schemeId));
        dataSet.Tables.Add(prices);
        connection.RespondToQuery(GetByIdSql, dataSet);

        var result = await repository.GetByIdAsync(schemeId);

        var price = Assert.Single(result!.Prices);
        Assert.Equal(currencyId, price.CurrencyId);
        Assert.Equal(123.45m, price.Price);
        Assert.Equal("£", price.CurrencySymbol);
    }

    [Fact]
    public async Task CreateAsync_NewPriceRow_IsInsertedWithEveryArgumentBoundByName()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToNonQuery(InsertPriceSql, 1);
        connection.RespondToQuery(GetByIdSql, SchemeTable(schemeId));

        var scheme = new CoreScheme
        {
            SchemeId = schemeId,
            YearId = 2026,
            Name = "Test Scheme",
            Prices = [new SchemeCurrencyPrice { CurrencyId = currencyId, Price = 99.99m }]
        };

        await repository.CreateAsync(scheme);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertPriceSql);
        Assert.Equal(schemeId, command.ParameterValue("@SchemeId"));
        Assert.Equal(currencyId, command.ParameterValue("@CurrencyId"));
        Assert.Equal(99.99m, command.ParameterValue("@Price"));
        Assert.NotEqual(Guid.Empty, scheme.Prices[0].SchemeCurrencyId);
    }

    [Fact]
    public async Task UpdateAsync_ExistingPriceRow_IsUpdatedRatherThanInserted()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var schemeCurrencyId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToNonQuery(UpdatePriceSql, 1);
        connection.RespondToQuery(GetByIdSql, SchemeTable(schemeId));

        var scheme = new CoreScheme
        {
            SchemeId = schemeId,
            YearId = 2026,
            Name = "Test Scheme",
            Prices = [new SchemeCurrencyPrice { SchemeCurrencyId = schemeCurrencyId, CurrencyId = Guid.NewGuid(), Price = 5m }]
        };

        await repository.UpdateAsync(scheme);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == UpdatePriceSql);
        Assert.Equal(schemeCurrencyId, command.ParameterValue("@SchemeCurrencyId"));
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == InsertPriceSql);
    }

    [Fact]
    public async Task GetByIdAsync_BuildsTheTestTreeFromTheChildResultSets()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var categoryItemId = Guid.NewGuid();

        var dataSet = new DataSet();
        dataSet.Tables.Add(SchemeTable(schemeId));
        dataSet.Tables.Add(EmptyPriceTable());
        dataSet.Tables.Add(TestTable(testId, schemeId));
        dataSet.Tables.Add(ItemTable("fldTestMethodItemId", "fldTestMethodItemTypeId", "fldTestMethodItemType", testId, "ELISA"));
        dataSet.Tables.Add(ItemTable("fldTestResultItemId", "fldTestResultItemTypeId", "fldTestResultItemType", testId, "Titre"));
        dataSet.Tables.Add(CategoryTable(categoryItemId, testId));
        dataSet.Tables.Add(CriterionTable(categoryItemId));
        connection.RespondToQuery(GetByIdSql, dataSet);

        var result = await repository.GetByIdAsync(schemeId);

        var test = Assert.Single(result!.Tests);
        Assert.Equal("Serology", test.TestType);
        Assert.Equal("Titre", Assert.Single(test.ResultItems).TestResultItemType);
        Assert.Equal("ELISA", Assert.Single(test.MethodItems).TestMethodItemType);
        var category = Assert.Single(test.Categories);
        Assert.Equal("Accuracy", category.Name);
        Assert.Equal("Within range", Assert.Single(category.Criteria).Name);
    }

    [Fact]
    public async Task UpdateAsync_StagedTestTree_IsInsertedAndRenumbered()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToNonQuery(InsertTestSql, 1);
        connection.RespondToNonQuery(InsertResultItemSql, 1);
        connection.RespondToQuery(GetByIdSql, SchemeTable(schemeId));

        var scheme = new CoreScheme
        {
            SchemeId = schemeId,
            YearId = 2026,
            Name = "Test Scheme",
            Tests =
            [
                new SchemeTest
                {
                    TestTypeId = Guid.NewGuid(),
                    ResultItems = [new SchemeTestResultItem { TestResultItemTypeId = Guid.NewGuid() }],
                }
            ],
        };

        await repository.UpdateAsync(scheme);

        var insertedTest = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTestSql);
        Assert.Equal(1, insertedTest.ParameterValue("@Order"));
        Assert.NotEqual(Guid.Empty, scheme.Tests[0].TestId);

        var insertedItem = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertResultItemSql);
        Assert.Equal(scheme.Tests[0].TestId, insertedItem.ParameterValue("@TestId"));
        Assert.Equal(1, insertedItem.ParameterValue("@Order"));
    }

    [Fact]
    public async Task UpdateAsync_TestRemovedFromTheTree_IsDeleted()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var removedTestId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToNonQuery("EXEC dbo.spdCriterionItem @CriterionItemId", 1);
        connection.RespondToNonQuery("EXEC dbo.spdCategoryItem @CategoryItemId", 1);
        connection.RespondToNonQuery("EXEC dbo.spdTestResultItem @TestResultItemId", 1);
        connection.RespondToNonQuery("EXEC dbo.spdTestMethodItem @TestMethodItemId", 1);
        connection.RespondToNonQuery("EXEC dbo.spdTest @TestId", 1);

        var dataSet = new DataSet();
        dataSet.Tables.Add(SchemeTable(schemeId));
        dataSet.Tables.Add(EmptyPriceTable());
        dataSet.Tables.Add(TestTable(removedTestId, schemeId));
        dataSet.Tables.Add(ItemTable("fldTestMethodItemId", "fldTestMethodItemTypeId", "fldTestMethodItemType", removedTestId, "ELISA"));
        dataSet.Tables.Add(ItemTable("fldTestResultItemId", "fldTestResultItemTypeId", "fldTestResultItemType", removedTestId, "Titre"));
        dataSet.Tables.Add(CategoryTable(Guid.NewGuid(), removedTestId));
        dataSet.Tables.Add(CriterionTable(Guid.NewGuid()));
        connection.RespondToQuery(GetByIdSql, dataSet);

        var scheme = new CoreScheme { SchemeId = schemeId, YearId = 2026, Name = "Test Scheme", Tests = [] };

        await repository.UpdateAsync(scheme);

        var deleted = Assert.Single(connection.ExecutedCommands, c => c.CommandText == "EXEC dbo.spdTest @TestId");
        Assert.Equal(removedTestId, deleted.ParameterValue("@TestId"));
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == "EXEC dbo.spdTestResultItem @TestResultItemId");
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == "EXEC dbo.spdTestMethodItem @TestMethodItemId");
    }

    [Fact]
    public async Task UpdateAsync_NewTabulation_IsInsertedWithItsItemLinks()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var tabulationId = Guid.NewGuid();
        var resultItemId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToNonQuery(InsertTabulationSql, 1);
        connection.RespondToNonQuery(InsertTabulationResultItemSql, 1);
        connection.RespondToQuery(GetByIdSql, SchemeTable(schemeId));

        var scheme = new CoreScheme
        {
            SchemeId = schemeId,
            YearId = 2026,
            Name = "Test Scheme",
            Tabulations =
            [
                new SchemeTabulation
                {
                    TabulationId = tabulationId,
                    Name = "Published",
                    AvailableToParticipants = true,
                    ResultItemIds = [resultItemId],
                }
            ],
        };

        await repository.UpdateAsync(scheme);

        var inserted = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTabulationSql);
        Assert.Equal(tabulationId, inserted.ParameterValue("@TabulationId"));
        Assert.Equal(schemeId, inserted.ParameterValue("@SchemeId"));
        Assert.Equal(true, inserted.ParameterValue("@AvailableToParticipants"));

        var link = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTabulationResultItemSql);
        Assert.Equal(resultItemId, link.ParameterValue("@ItemId"));
        Assert.Equal(tabulationId, link.ParameterValue("@TabulationId"));
    }

    [Fact]
    public async Task UpdateAsync_TabulationRemovedFromTheTab_IsDeleted()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var removedTabulationId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToNonQuery("EXEC dbo.spdTabulation @TabulationId", 1);

        var dataSet = new DataSet();
        dataSet.Tables.Add(SchemeTable(schemeId));
        dataSet.Tables.Add(EmptyPriceTable());
        dataSet.Tables.Add(EmptyTestTable());
        dataSet.Tables.Add(EmptyItemTable("fldTestMethodItemId", "fldTestMethodItemTypeId", "fldTestMethodItemType"));
        dataSet.Tables.Add(EmptyItemTable("fldTestResultItemId", "fldTestResultItemTypeId", "fldTestResultItemType"));
        dataSet.Tables.Add(EmptyCategoryTable());
        dataSet.Tables.Add(EmptyCriterionTable());
        dataSet.Tables.Add(TabulationTable(removedTabulationId, schemeId));
        dataSet.Tables.Add(EmptyTabulationLinkTable("fldTabulationTestMethodItemId", "fldTestMethodItemId"));
        dataSet.Tables.Add(EmptyTabulationLinkTable("fldTabulationTestResultItemId", "fldTestResultItemId"));
        connection.RespondToQuery(GetByIdSql, dataSet);

        var scheme = new CoreScheme { SchemeId = schemeId, YearId = 2026, Name = "Test Scheme", Tabulations = [] };

        await repository.UpdateAsync(scheme);

        var deleted = Assert.Single(connection.ExecutedCommands, c => c.CommandText == "EXEC dbo.spdTabulation @TabulationId");
        Assert.Equal(removedTabulationId, deleted.ParameterValue("@TabulationId"));
    }

    [Fact]
    public async Task GetByIdAsync_BuildsTabulationsWithTheirItemLinks()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var tabulationId = Guid.NewGuid();
        var resultItemId = Guid.NewGuid();

        var resultLinks = EmptyTabulationLinkTable("fldTabulationTestResultItemId", "fldTestResultItemId");
        resultLinks.Rows.Add(Guid.NewGuid(), tabulationId, resultItemId);

        var dataSet = new DataSet();
        dataSet.Tables.Add(SchemeTable(schemeId));
        dataSet.Tables.Add(EmptyPriceTable());
        dataSet.Tables.Add(EmptyTestTable());
        dataSet.Tables.Add(EmptyItemTable("fldTestMethodItemId", "fldTestMethodItemTypeId", "fldTestMethodItemType"));
        dataSet.Tables.Add(EmptyItemTable("fldTestResultItemId", "fldTestResultItemTypeId", "fldTestResultItemType"));
        dataSet.Tables.Add(EmptyCategoryTable());
        dataSet.Tables.Add(EmptyCriterionTable());
        dataSet.Tables.Add(TabulationTable(tabulationId, schemeId));
        dataSet.Tables.Add(EmptyTabulationLinkTable("fldTabulationTestMethodItemId", "fldTestMethodItemId"));
        dataSet.Tables.Add(resultLinks);
        connection.RespondToQuery(GetByIdSql, dataSet);

        var result = await repository.GetByIdAsync(schemeId);

        var tabulation = Assert.Single(result!.Tabulations);
        Assert.Equal("Published", tabulation.Name);
        Assert.Equal(resultItemId, Assert.Single(tabulation.ResultItemIds));
        Assert.Empty(tabulation.MethodItemIds);
    }

    public static TheoryData<string, string, bool> ItemAuditProcedures => new()
    {
        { "Tests", AuditTestSql, true },
        { "TestMethodItems", AuditMethodItemSql, true },
        { "TestResultItems", AuditResultItemSql, true },
        { "CategoryItems", AuditCategoryItemSql, false },
        { "CriterionItems", AuditCriterionItemSql, false },
    };

    [Theory]
    [MemberData(nameof(ItemAuditProcedures))]
    public async Task UpdateAsync_WithAudit_ItemRowsAreAuditedWithTheirOwnArguments(string kind, string commandText, bool hasTypeUpdate)
    {
        var inserted = new SchemeItemAudit(Guid.NewGuid(), SchemeAuditOperation.Insert, Guid.NewGuid(), false, 1, false);
        var updated = new SchemeItemAudit(Guid.NewGuid(), SchemeAuditOperation.Update, Guid.NewGuid(), true, 3, true);
        var deleted = new SchemeItemAudit(Guid.NewGuid(), SchemeAuditOperation.Delete, Guid.NewGuid(), false, 2, false);
        var scheme = new CoreScheme { SchemeId = Guid.NewGuid(), YearId = 2026, Name = "Test Scheme" };

        var (connection, _) = await RunUpdateAsync(scheme, Audit(ItemPlan(kind, [inserted, updated, deleted])));

        var commands = connection.ExecutedCommands.Where(c => c.CommandText == commandText).ToList();
        Assert.Equal(3, commands.Count);
        AssertItemAudit(commands[0], inserted, hasTypeUpdate, isDeleted: false);
        AssertItemAudit(commands[1], updated, hasTypeUpdate, isDeleted: false);
        AssertItemAudit(commands[2], deleted, hasTypeUpdate, isDeleted: true);
    }

    [Fact]
    public async Task UpdateAsync_WithAudit_TabulationRowsCarryOnlyTheChangedFieldFlags()
    {
        var renamed = new SchemeTabulationAudit(Guid.NewGuid(), SchemeAuditOperation.Update, "Renamed", true, false, false);
        var narrowed = new SchemeTabulationAudit(Guid.NewGuid(), SchemeAuditOperation.Update, "Same", false, true, true);
        var deleted = new SchemeTabulationAudit(Guid.NewGuid(), SchemeAuditOperation.Delete, "Gone", false, false, false);
        var plan = EmptyPlan with { Tabulations = [renamed, narrowed, deleted] };
        var scheme = new CoreScheme { SchemeId = Guid.NewGuid(), YearId = 2026, Name = "Test Scheme" };

        var (connection, _) = await RunUpdateAsync(scheme, Audit(plan));

        var commands = connection.ExecutedCommands.Where(c => c.CommandText == AuditTabulationSql).ToList();
        Assert.Equal(3, commands.Count);
        AssertTabulationAudit(commands[0], renamed, isDeleted: false);
        AssertTabulationAudit(commands[1], narrowed, isDeleted: false);
        AssertTabulationAudit(commands[2], deleted, isDeleted: true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_WithAudit_LinkRowsAreAuditedAsInsertsAndDeletesOnly(bool methodLinks)
    {
        var tabulationId = Guid.NewGuid();
        var inserted = new SchemeTabulationLinkAudit(Guid.NewGuid(), SchemeAuditOperation.Insert, tabulationId, Guid.NewGuid());
        var deleted = new SchemeTabulationLinkAudit(Guid.NewGuid(), SchemeAuditOperation.Delete, tabulationId, Guid.NewGuid());
        SchemeTabulationLinkAudit[] links = [inserted, deleted];
        var plan = methodLinks ? EmptyPlan with { MethodLinks = links } : EmptyPlan with { ResultLinks = links };
        var scheme = new CoreScheme { SchemeId = Guid.NewGuid(), YearId = 2026, Name = "Test Scheme" };

        var (connection, _) = await RunUpdateAsync(scheme, Audit(plan));

        // The command text carries the literal "...IdUpdate = 0" arguments, so matching it exactly asserts them.
        var commands = connection.ExecutedCommands.Where(c => c.CommandText == (methodLinks ? AuditMethodLinkSql : AuditResultLinkSql)).ToList();
        Assert.Equal(2, commands.Count);
        AssertLinkAudit(commands[0], inserted, isDeleted: false);
        AssertLinkAudit(commands[1], deleted, isDeleted: true);
    }

    [Fact]
    public async Task UpdateAsync_WithAudit_AuditRowsRunAfterTheSchemeUpdateAndBeforeEveryChildWrite()
    {
        var scenario = FullScenario();

        var (connection, result) = await RunUpdateAsync(scenario.Scheme, Audit(scenario.Plan), scenario.Stored);

        Assert.NotNull(result);
        var writes = Writes(connection).Select(c => c.CommandText).ToList();
        string[] expectedAudits =
        [
            AuditTestSql, AuditMethodItemSql, AuditResultItemSql, AuditCategoryItemSql,
            AuditCriterionItemSql, AuditTabulationSql, AuditMethodLinkSql, AuditResultLinkSql,
        ];

        Assert.Equal(UpdateSql, writes[0]);
        Assert.Equal(expectedAudits, writes.Skip(1).Take(expectedAudits.Length));
        var childWrites = writes.Skip(1 + expectedAudits.Length).ToList();
        Assert.DoesNotContain(childWrites, w => w.StartsWith("EXEC dbo.spiAudit", StringComparison.Ordinal));
        Assert.Contains(UpdatePriceSql, childWrites);
        Assert.Contains(InsertTestSql, childWrites);
        Assert.Contains(childWrites, w => w.StartsWith("EXEC dbo.spuTabulation ", StringComparison.Ordinal));
        Assert.Contains(DeleteTabulationMethodItemSql, childWrites);
        Assert.Contains(InsertTabulationResultItemSql, childWrites);
        Assert.Contains("EXEC dbo.spdTest @TestId", childWrites);
    }

    [Fact]
    public async Task UpdateAsync_WithAudit_NewLinkIsInsertedUnderTheIdItsAuditRowUsed()
    {
        var scenario = FullScenario();

        var (connection, _) = await RunUpdateAsync(scenario.Scheme, Audit(scenario.Plan), scenario.Stored);

        var audit = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditResultLinkSql);
        var insert = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTabulationResultItemSql);
        Assert.Equal(scenario.PlannedLinkId, audit.ParameterValue("@LinkId"));
        Assert.Equal(scenario.PlannedLinkId, insert.ParameterValue("@LinkId"));
        Assert.Equal(scenario.NewResultItemId, insert.ParameterValue("@ItemId"));
    }

    [Fact]
    public async Task UpdateAsync_WithAudit_RemovedLinkIsDeletedByItsStoredId()
    {
        var scenario = FullScenario();

        var (connection, _) = await RunUpdateAsync(scenario.Scheme, Audit(scenario.Plan), scenario.Stored);

        var delete = Assert.Single(connection.ExecutedCommands, c => c.CommandText == DeleteTabulationMethodItemSql);
        Assert.Equal(scenario.StoredMethodLinkId, delete.ParameterValue("@LinkId"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_WithAudit_LinkNotInThePlanGetsAFreshId(bool plannedForAnotherItem)
    {
        var tabulationId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var plannedLinkId = Guid.NewGuid();
        var planned = plannedForAnotherItem
            ? new SchemeTabulationLinkAudit(plannedLinkId, SchemeAuditOperation.Insert, tabulationId, Guid.NewGuid())
            : new SchemeTabulationLinkAudit(plannedLinkId, SchemeAuditOperation.Delete, tabulationId, itemId);
        var scheme = SchemeWithNewTabulation(tabulationId, itemId);

        var (connection, _) = await RunUpdateAsync(scheme, Audit(EmptyPlan with { ResultLinks = [planned] }));

        var insert = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTabulationResultItemSql);
        var linkId = Assert.IsType<Guid>(insert.ParameterValue("@LinkId"));
        Assert.NotEqual(Guid.Empty, linkId);
        Assert.NotEqual(plannedLinkId, linkId);
    }

    [Fact]
    public async Task UpdateAsync_WithoutAudit_RunsNoAuditCommandAndLinksGetAFreshId()
    {
        var tabulationId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var scheme = SchemeWithNewTabulation(tabulationId, itemId);

        var (connection, result) = await RunUpdateAsync(scheme, audit: null);

        Assert.NotNull(result);
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText.Contains("spiAudit", StringComparison.Ordinal));
        var insert = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTabulationResultItemSql);
        Assert.NotEqual(Guid.Empty, Assert.IsType<Guid>(insert.ParameterValue("@LinkId")));
    }

    [Fact]
    public async Task UpdateAsync_ExistingTabulationLinksNoLongerRequested_AreDeleted()
    {
        var scenario = FullScenario();

        var (connection, _) = await RunUpdateAsync(scenario.Scheme, audit: null, scenario.Stored);

        var deletedMethodLink = Assert.Single(connection.ExecutedCommands, c => c.CommandText == DeleteTabulationMethodItemSql);
        Assert.Equal(scenario.StoredMethodLinkId, deletedMethodLink.ParameterValue("@LinkId"));
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == DeleteTabulationResultItemSql);
        Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTabulationResultItemSql);
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == InsertTabulationMethodItemSql);
    }

    [Fact]
    public async Task UpdateAsync_StoredResultLinkRemoved_IsDeleted()
    {
        var scenario = FullScenario();
        scenario.Scheme.Tabulations[0].ResultItemIds = [];

        var (connection, _) = await RunUpdateAsync(scenario.Scheme, audit: null, scenario.Stored);

        var deleted = Assert.Single(connection.ExecutedCommands, c => c.CommandText == DeleteTabulationResultItemSql);
        Assert.Equal(scenario.StoredResultLinkId, deleted.ParameterValue("@LinkId"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_EveryWriteJoinsTheTransactionAndItIsCommitted(bool audited)
    {
        var scenario = FullScenario();

        var (connection, result) = await RunUpdateAsync(scenario.Scheme, audited ? Audit(scenario.Plan) : null, scenario.Stored);

        Assert.NotNull(result);
        var transaction = Assert.IsType<FakeDbTransaction>(connection.LastTransaction);
        Assert.True(transaction.Committed);
        var writes = Writes(connection).ToList();
        Assert.True(writes.Count > 8);
        Assert.All(writes, c => Assert.Same(transaction, c.EnlistedTransaction));
        Assert.All(connection.ExecutedCommands.Except(writes), c => Assert.Null(c.EnlistedTransaction));
    }

    [Fact]
    public async Task UpdateAsync_AuditCommandIsEnlistedInTheTransaction()
    {
        var scenario = FullScenario();

        var (connection, _) = await RunUpdateAsync(scenario.Scheme, Audit(scenario.Plan), scenario.Stored);

        var audits = connection.ExecutedCommands.Where(c => c.CommandText.StartsWith("EXEC dbo.spiAudit", StringComparison.Ordinal)).ToList();
        Assert.Equal(8, audits.Count);
        Assert.All(audits, c => Assert.Same(connection.LastTransaction, c.EnlistedTransaction));
    }

    [Fact]
    public async Task UpdateAsync_ACommandFailsAfterTheAuditRowsAreWritten_TheTransactionIsNotCommitted()
    {
        var (repository, connection) = CreateRepository();
        var scheme = new CoreScheme
        {
            SchemeId = Guid.NewGuid(),
            YearId = 2026,
            Name = "Test Scheme",
            Prices = [new SchemeCurrencyPrice { SchemeCurrencyId = Guid.NewGuid(), CurrencyId = Guid.NewGuid(), Price = 5m }]
        };
        var plan = EmptyPlan with { Tests = [new SchemeItemAudit(Guid.NewGuid(), SchemeAuditOperation.Delete, Guid.NewGuid(), false, 1, false)] };
        connection.RespondToQuery(GetByIdSql, SchemeTable(scheme.SchemeId));
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToNonQuery(AuditTestSql, 1);

        // The price update has no response configured, so it throws part-way through the save.
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(scheme, Audit(plan)));

        var transaction = Assert.IsType<FakeDbTransaction>(connection.LastTransaction);
        Assert.False(transaction.Committed);
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == AuditTestSql);
        Assert.Equal(UpdatePriceSql, connection.ExecutedCommands[^1].CommandText);
    }

    [Fact]
    public async Task UpdateAsync_SchemeUpdateAffectsNoRows_WritesNothingElseAndDoesNotCommit()
    {
        var (repository, connection) = CreateRepository();
        var scheme = new CoreScheme { SchemeId = Guid.NewGuid(), YearId = 2026, Name = "Test Scheme" };
        var plan = EmptyPlan with { Tests = [new SchemeItemAudit(Guid.NewGuid(), SchemeAuditOperation.Delete, Guid.NewGuid(), false, 1, false)] };
        connection.RespondToQuery(GetByIdSql, SchemeTable(scheme.SchemeId));
        connection.RespondToNonQuery(UpdateSql, 0);

        var result = await repository.UpdateAsync(scheme, Audit(plan));

        Assert.Null(result);
        Assert.False(Assert.IsType<FakeDbTransaction>(connection.LastTransaction).Committed);
        Assert.Equal(UpdateSql, Assert.Single(Writes(connection)).CommandText);
    }

    [Fact]
    public async Task CreateAsync_RunsNoAuditCommandAndNoTransaction()
    {
        var (repository, connection) = CreateRepository();
        var tabulationId = Guid.NewGuid();
        var scheme = SchemeWithNewTabulation(tabulationId, Guid.NewGuid());
        scheme.Tests = [new SchemeTest { TestTypeId = Guid.NewGuid() }];
        connection.DefaultNonQueryResult = 1;
        connection.RespondToQuery(GetByIdSql, SchemeTable(scheme.SchemeId));

        var result = await repository.CreateAsync(scheme);

        Assert.Equal(scheme.SchemeId, result.SchemeId);
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText.Contains("spiAudit", StringComparison.Ordinal));
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == InsertTestSql);
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == InsertTabulationResultItemSql);
        Assert.Null(connection.LastTransaction);
        Assert.All(connection.ExecutedCommands, c => Assert.Null(c.EnlistedTransaction));
    }

    private static SchemeAuditPlan EmptyPlan => new([], [], [], [], [], [], [], []);

    private static SchemeAuditWrite Audit(SchemeAuditPlan plan) => new(AuditWho, AuditWhy, plan);

    private static SchemeAuditPlan ItemPlan(string kind, SchemeItemAudit[] rows) => kind switch
    {
        "Tests" => EmptyPlan with { Tests = rows },
        "TestMethodItems" => EmptyPlan with { TestMethodItems = rows },
        "TestResultItems" => EmptyPlan with { TestResultItems = rows },
        "CategoryItems" => EmptyPlan with { CategoryItems = rows },
        "CriterionItems" => EmptyPlan with { CriterionItems = rows },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    // Every command that is neither a read nor part of the re-read, in execution order.
    private static IEnumerable<FakeDbCommand> Writes(FakeDbConnection connection) =>
        connection.ExecutedCommands.Where(c => !c.CommandText.StartsWith("EXEC dbo.spg", StringComparison.Ordinal));

    private static async Task<(FakeDbConnection Connection, CoreScheme? Result)> RunUpdateAsync(CoreScheme scheme, SchemeAuditWrite? audit, DataSet? stored = null)
    {
        var (repository, connection) = CreateRepository();
        connection.DefaultNonQueryResult = 1;
        if (stored is null)
        {
            connection.RespondToQuery(GetByIdSql, SchemeTable(scheme.SchemeId));
        }
        else
        {
            connection.RespondToQuery(GetByIdSql, stored);
        }

        var result = await repository.UpdateAsync(scheme, audit);
        return (connection, result);
    }

    private static CoreScheme SchemeWithNewTabulation(Guid tabulationId, Guid resultItemId) => new()
    {
        SchemeId = Guid.NewGuid(),
        YearId = 2026,
        Name = "Test Scheme",
        Tabulations = [new SchemeTabulation { TabulationId = tabulationId, Name = "Published", ResultItemIds = [resultItemId] }],
    };

    private static void AssertItemAudit(FakeDbCommand command, SchemeItemAudit expected, bool hasTypeUpdate, bool isDeleted)
    {
        Assert.Equal(expected.Id, command.ParameterValue("@Id"));
        Assert.Equal(AuditWho, command.ParameterValue("@AuditWho"));
        Assert.Equal(AuditWhy, command.ParameterValue("@AuditWhy"));
        Assert.Equal(expected.TypeId, command.ParameterValue("@TypeId"));
        Assert.Equal(expected.Order, command.ParameterValue("@Order"));
        Assert.Equal(expected.OrderChanged, command.ParameterValue("@OrderChanged"));
        Assert.Equal(isDeleted, command.ParameterValue("@IsDeleted"));
        if (hasTypeUpdate)
        {
            Assert.Equal(expected.TypeChanged, command.ParameterValue("@TypeChanged"));
        }
    }

    private static void AssertTabulationAudit(FakeDbCommand command, SchemeTabulationAudit expected, bool isDeleted)
    {
        Assert.Equal(expected.Id, command.ParameterValue("@Id"));
        Assert.Equal(AuditWho, command.ParameterValue("@AuditWho"));
        Assert.Equal(AuditWhy, command.ParameterValue("@AuditWhy"));
        Assert.Equal(expected.Name, command.ParameterValue("@Name"));
        Assert.Equal(expected.NameChanged, command.ParameterValue("@NameChanged"));
        Assert.Equal(expected.IntendedResultsOnly, command.ParameterValue("@IntendedResultsOnly"));
        Assert.Equal(expected.IntendedResultsOnlyChanged, command.ParameterValue("@IntendedResultsOnlyChanged"));
        Assert.Equal(isDeleted, command.ParameterValue("@IsDeleted"));
    }

    private static void AssertLinkAudit(FakeDbCommand command, SchemeTabulationLinkAudit expected, bool isDeleted)
    {
        Assert.Equal(expected.LinkId, command.ParameterValue("@LinkId"));
        Assert.Equal(AuditWho, command.ParameterValue("@AuditWho"));
        Assert.Equal(AuditWhy, command.ParameterValue("@AuditWhy"));
        Assert.Equal(expected.TabulationId, command.ParameterValue("@TabulationId"));
        Assert.Equal(expected.ItemId, command.ParameterValue("@ItemId"));
        Assert.Equal(isDeleted, command.ParameterValue("@IsDeleted"));
    }

    private sealed record Scenario(
        CoreScheme Scheme,
        DataSet Stored,
        SchemeAuditPlan Plan,
        Guid PlannedLinkId,
        Guid NewResultItemId,
        Guid StoredMethodLinkId,
        Guid StoredResultLinkId);

    // Stored: one test (to be deleted) and one tabulation linked to a method item and a result item.
    // Saved: a new test, the same tabulation kept with its method link removed and a second result link added.
    private static Scenario FullScenario()
    {
        var schemeId = Guid.NewGuid();
        var storedTestId = Guid.NewGuid();
        var storedCategoryId = Guid.NewGuid();
        var tabulationId = Guid.NewGuid();
        var storedMethodLinkId = Guid.NewGuid();
        var storedResultLinkId = Guid.NewGuid();
        var storedMethodItemId = Guid.NewGuid();
        var keptResultItemId = Guid.NewGuid();
        var newResultItemId = Guid.NewGuid();
        var plannedLinkId = Guid.NewGuid();

        var methodLinks = EmptyTabulationLinkTable("fldTabulationTestMethodItemId", "fldTestMethodItemId");
        methodLinks.Rows.Add(storedMethodLinkId, tabulationId, storedMethodItemId);
        var resultLinks = EmptyTabulationLinkTable("fldTabulationTestResultItemId", "fldTestResultItemId");
        resultLinks.Rows.Add(storedResultLinkId, tabulationId, keptResultItemId);

        var stored = new DataSet();
        stored.Tables.Add(SchemeTable(schemeId));
        stored.Tables.Add(EmptyPriceTable());
        stored.Tables.Add(TestTable(storedTestId, schemeId));
        stored.Tables.Add(ItemTable("fldTestMethodItemId", "fldTestMethodItemTypeId", "fldTestMethodItemType", storedTestId, "ELISA"));
        stored.Tables.Add(ItemTable("fldTestResultItemId", "fldTestResultItemTypeId", "fldTestResultItemType", storedTestId, "Titre"));
        stored.Tables.Add(CategoryTable(storedCategoryId, storedTestId));
        stored.Tables.Add(CriterionTable(storedCategoryId));
        stored.Tables.Add(TabulationTable(tabulationId, schemeId));
        stored.Tables.Add(methodLinks);
        stored.Tables.Add(resultLinks);

        var newTestId = Guid.NewGuid();
        var scheme = new CoreScheme
        {
            SchemeId = schemeId,
            YearId = 2026,
            Name = "Test Scheme",
            Prices = [new SchemeCurrencyPrice { SchemeCurrencyId = Guid.NewGuid(), CurrencyId = Guid.NewGuid(), Price = 5m }],
            Tests =
            [
                new SchemeTest
                {
                    TestId = newTestId,
                    TestTypeId = Guid.NewGuid(),
                    ResultItems = [new SchemeTestResultItem { TestResultItemId = newResultItemId, TestResultItemTypeId = Guid.NewGuid() }],
                }
            ],
            Tabulations =
            [
                new SchemeTabulation
                {
                    TabulationId = tabulationId,
                    Name = "Renamed",
                    MethodItemIds = [],
                    ResultItemIds = [keptResultItemId, newResultItemId],
                }
            ],
        };

        SchemeItemAudit Item(Guid id, SchemeAuditOperation operation) => new(id, operation, Guid.NewGuid(), false, 1, false);
        var plan = new SchemeAuditPlan(
            Tests: [Item(storedTestId, SchemeAuditOperation.Delete)],
            TestMethodItems: [Item(Guid.NewGuid(), SchemeAuditOperation.Delete)],
            TestResultItems: [Item(newResultItemId, SchemeAuditOperation.Insert)],
            CategoryItems: [Item(storedCategoryId, SchemeAuditOperation.Delete)],
            CriterionItems: [Item(Guid.NewGuid(), SchemeAuditOperation.Delete)],
            Tabulations: [new SchemeTabulationAudit(tabulationId, SchemeAuditOperation.Update, "Renamed", true, false, false)],
            MethodLinks: [new SchemeTabulationLinkAudit(storedMethodLinkId, SchemeAuditOperation.Delete, tabulationId, storedMethodItemId)],
            ResultLinks: [new SchemeTabulationLinkAudit(plannedLinkId, SchemeAuditOperation.Insert, tabulationId, newResultItemId)]);

        return new Scenario(scheme, stored, plan, plannedLinkId, newResultItemId, storedMethodLinkId, storedResultLinkId);
    }

    private static DataTable EmptyTestTable() => TestTable(Guid.Empty, Guid.Empty, includeRow: false);

    private static DataTable EmptyItemTable(string idColumn, string typeIdColumn, string nameColumn)
    {
        var table = ItemTable(idColumn, typeIdColumn, nameColumn, Guid.Empty, string.Empty);
        table.Rows.Clear();
        return table;
    }

    private static DataTable EmptyCategoryTable()
    {
        var table = CategoryTable(Guid.Empty, Guid.Empty);
        table.Rows.Clear();
        return table;
    }

    private static DataTable EmptyCriterionTable()
    {
        var table = CriterionTable(Guid.Empty);
        table.Rows.Clear();
        return table;
    }

    private static DataTable TabulationTable(Guid tabulationId, Guid schemeId)
    {
        var table = new DataTable();
        table.Columns.Add("fldTabulationId", typeof(Guid));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldIntendedResultsOnly", typeof(bool));
        table.Columns.Add("fldSingleParticipantTabulation", typeof(bool));
        table.Columns.Add("fldShowRatings", typeof(bool));
        table.Columns.Add("fldAvailableToParticipants", typeof(bool));
        table.Columns.Add("fldAvailableToViewers", typeof(bool));
        table.Rows.Add(tabulationId, schemeId, "Published", false, false, false, true, true);
        return table;
    }

    private static DataTable EmptyTabulationLinkTable(string linkIdColumn, string itemIdColumn)
    {
        var table = new DataTable();
        table.Columns.Add(linkIdColumn, typeof(Guid));
        table.Columns.Add("fldTabulationId", typeof(Guid));
        table.Columns.Add(itemIdColumn, typeof(Guid));
        return table;
    }

    private static DataTable EmptyPriceTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldSchemeCurrencyId", typeof(Guid));
        table.Columns.Add("fldCurrencyId", typeof(Guid));
        table.Columns.Add("fldPrice", typeof(decimal));
        return table;
    }

    private static DataTable TestTable(Guid testId, Guid schemeId, bool includeRow = true)
    {
        var table = new DataTable();
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldTestTypeId", typeof(Guid));
        table.Columns.Add("fldTestType", typeof(string));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldOrder", typeof(int));
        if (includeRow)
        {
            table.Rows.Add(testId, Guid.NewGuid(), "Serology", schemeId, 1);
        }

        return table;
    }

    private static DataTable ItemTable(string idColumn, string typeIdColumn, string nameColumn, Guid testId, string name)
    {
        var table = new DataTable();
        table.Columns.Add(idColumn, typeof(Guid));
        table.Columns.Add(typeIdColumn, typeof(Guid));
        table.Columns.Add(nameColumn, typeof(string));
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldOrder", typeof(int));
        table.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), name, testId, 1);
        return table;
    }

    private static DataTable CategoryTable(Guid categoryItemId, Guid testId)
    {
        var table = new DataTable();
        table.Columns.Add("fldCategoryItemId", typeof(Guid));
        table.Columns.Add("fldCategoryItemTypeId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldOrder", typeof(int));
        table.Rows.Add(categoryItemId, Guid.NewGuid(), "Accuracy", testId, 1);
        return table;
    }

    private static DataTable CriterionTable(Guid categoryItemId)
    {
        var table = new DataTable();
        table.Columns.Add("fldCriterionItemId", typeof(Guid));
        table.Columns.Add("fldCriterionItemTypeId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldCategoryItemId", typeof(Guid));
        table.Columns.Add("fldOrder", typeof(int));
        table.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "Within range", categoryItemId, 1);
        return table;
    }
}
