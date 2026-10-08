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
