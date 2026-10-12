using System.Data;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

public class DistributionRepositoryTests
{
    public DistributionRepositoryTests() => DapperColumnMappings.Register();

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new DistributionRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetMonthlyDistributionSummariesAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var monthlyDistributionId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        table.Columns.Add("fldMonthId", typeof(int));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldNumberOfSchemes", typeof(int));
        table.Columns.Add("fldNumberOfSampleNumbersDefined", typeof(int));
        table.Columns.Add("fldNumberOfPrepComplete", typeof(int));
        table.Columns.Add("fldNumberOfParticipants", typeof(int));
        table.Columns.Add("fldNumberOfPackagingComplete", typeof(int));
        table.Columns.Add("fldNumberOfResultsEntered", typeof(int));
        table.Columns.Add("fldNumberOfTabulations", typeof(int));
        table.Columns.Add("fldNumberOfCompleteTabulations", typeof(int));
        table.Rows.Add(monthlyDistributionId, 4, 2026, 3, 2, 1, 10, 5, 4, 2, 1);
        connection.RespondToQuery("EXEC dbo.spgaMonthlyDistributionInfo", table);

        var result = await repository.GetMonthlyDistributionSummariesAsync();

        Assert.Single(result);
        Assert.Equal(monthlyDistributionId, result[0].MonthlyDistributionId);
        Assert.Equal(4, result[0].MonthId);
        Assert.Equal(2026, result[0].YearId);
        Assert.Equal(3, result[0].NumberOfSchemes);
        Assert.Equal(10, result[0].NumberOfParticipants);
    }

    [Fact]
    public async Task GetDistributionYearsAsync_ReturnsMappedYears()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldMonthId", typeof(int));
        table.Rows.Add(2026, 6);
        connection.RespondToQuery("EXEC dbo.spgaDistributionYearInfo", table);

        var result = await repository.GetDistributionYearsAsync();

        Assert.Single(result);
        Assert.Equal(2026, result[0].YearId);
        Assert.Equal(6, result[0].MonthId);
    }

    [Fact]
    public async Task GetMonthlyDistributionSchedulesAsync_MonthNotInitialised_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var header = new DataTable();
        header.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        header.Columns.Add("fldYearId", typeof(int));
        header.Columns.Add("fldMonthId", typeof(int));
        var schemes = new DataTable();
        schemes.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        var participants = new DataTable();
        participants.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        participants.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        var dataSet = new DataSet();
        dataSet.Tables.Add(header);
        dataSet.Tables.Add(schemes);
        dataSet.Tables.Add(participants);
        connection.RespondToQuery("EXEC dbo.spgMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId", dataSet);

        var result = await repository.GetMonthlyDistributionSchedulesAsync(2026, 4);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMonthlyDistributionSchedulesAsync_MonthInitialised_AggregatesParticipantsOntoSchemes()
    {
        var (repository, connection) = CreateRepository();
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();

        var header = new DataTable();
        header.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        header.Columns.Add("fldYearId", typeof(int));
        header.Columns.Add("fldMonthId", typeof(int));
        header.Rows.Add(monthlyDistributionId, 2026, 4);

        var schemes = new DataTable();
        schemes.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        schemes.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        schemes.Columns.Add("fldSchemeId", typeof(Guid));
        schemes.Columns.Add("SchemeIdentifier", typeof(string));
        schemes.Columns.Add("SchemeName", typeof(string));
        schemes.Columns.Add("fldDistributionReference", typeof(string));
        schemes.Columns.Add("fldDistributionReferenceSuffix", typeof(string));
        schemes.Columns.Add("fldScheduleCode", typeof(string));
        schemes.Columns.Add("fldDistributionDate", typeof(DateTime));
        schemes.Columns.Add("fldOverseasPostingDate", typeof(DateTime));
        schemes.Columns.Add("fldDeadlineDate", typeof(DateTime));
        schemes.Columns.Add("fldResultsIssueTargetDate", typeof(DateTime));
        schemes.Columns.Add("fldComments", typeof(string));
        schemes.Columns.Add("fldHasIntendedResults", typeof(bool));
        schemes.Columns.Add("fldHasSampleNumbersDefined", typeof(bool));
        schemes.Columns.Add("fldIsCancelled", typeof(bool));
        schemes.Columns.Add("fldIsAsAvailable", typeof(bool));
        schemes.Columns.Add("fldStoreRatings", typeof(bool));
        schemes.Columns.Add("fldSchemeVersionDate", typeof(DateTime));
        schemes.Rows.Add(
            schemeId, monthlyDistributionId, Guid.NewGuid(), "S001", "Test Scheme", "D26-01", "A", "BA",
            new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 4, 5), new DateTime(2026, 4, 10),
            "Some comments", false, false, false, false, false, new DateTime(2026, 1, 1));

        var participants = new DataTable();
        participants.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        participants.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        participants.Rows.Add(schemeId, 3);
        participants.Rows.Add(schemeId, 2);

        var dataSet = new DataSet();
        dataSet.Tables.Add(header);
        dataSet.Tables.Add(schemes);
        dataSet.Tables.Add(participants);
        connection.RespondToQuery("EXEC dbo.spgMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId", dataSet);

        var result = await repository.GetMonthlyDistributionSchedulesAsync(2026, 4);

        Assert.NotNull(result);
        Assert.Equal(monthlyDistributionId, result!.Value.MonthlyDistributionId);
        var scheme = Assert.Single(result.Value.Schemes);
        Assert.Equal(2, scheme.ParticipantCount);
        Assert.Equal(5, scheme.TotalSetsOfSamplesRequired);
        Assert.Equal("D26-01A/BA", scheme.DistributionReferenceFull);
    }

    [Fact]
    public async Task UpdateMonthlyDistributionSchemesAsync_NoSchemes_DoesNotOpenATransaction()
    {
        var (repository, connection) = CreateRepository();

        await repository.UpdateMonthlyDistributionSchemesAsync([]);

        Assert.Empty(connection.ExecutedCommands);
        Assert.Null(connection.LastTransaction);
    }

    [Fact]
    public async Task UpdateMonthlyDistributionSchemesAsync_ExecutesOneCommandPerSchemeAndCommits()
    {
        var (repository, connection) = CreateRepository();
        const string sql = """
            EXEC dbo.spuMonthlyDistributionScheme
                @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                @MonthlyDistributionId = @MonthlyDistributionId,
                @SchemeId = @SchemeId,
                @MonthlyDistributionReference = @DistributionReference,
                @MonthlyDistributionReferenceSuffix = @DistributionReferenceSuffix,
                @MonthlyDistributionDate = @DistributionDate,
                @DeadlineDate = @DeadlineDate,
                @OverseasPostingDate = @OverseasPostingDate,
                @ResultsIssueTargetDate = @ResultsIssueTargetDate,
                @Comments = @Comments,
                @HasIntendedResults = @HasIntendedResults,
                @HasSampleNumbersDefined = @HasSampleNumbersDefined,
                @IsCancelled = @IsCancelled,
                @StoreRatings = @StoreRatings,
                @SchemeVersionDate = @SchemeVersionDate
            """;
        connection.RespondToNonQuery(sql, 1);

        var schemes = new List<PTL.Core.Distribution.MonthlyDistributionSchemeEntity>
        {
            new() { MonthlyDistributionSchemeId = Guid.NewGuid() },
            new() { MonthlyDistributionSchemeId = Guid.NewGuid() },
        };

        await repository.UpdateMonthlyDistributionSchemesAsync(schemes);

        Assert.Equal(2, connection.ExecutedCommands.Count(c => c.CommandText == sql));
        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.Committed);
    }

    private const string GetSchemeSql = "EXEC dbo.spgMonthlyDistributionScheme @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";
    private const string GetIntendedResultsSql = "EXEC dbo.spgIntendedResults @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";

    private static DataTable SchemeTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("SchemeIdentifier", typeof(string));
        table.Columns.Add("SchemeName", typeof(string));
        table.Columns.Add("fldDistributionReference", typeof(string));
        table.Columns.Add("fldDistributionReferenceSuffix", typeof(string));
        table.Columns.Add("fldScheduleCode", typeof(string));
        table.Columns.Add("fldDistributionDate", typeof(DateTime));
        table.Columns.Add("fldOverseasPostingDate", typeof(DateTime));
        table.Columns.Add("fldDeadlineDate", typeof(DateTime));
        table.Columns.Add("fldResultsIssueTargetDate", typeof(DateTime));
        table.Columns.Add("fldComments", typeof(string));
        table.Columns.Add("fldHasIntendedResults", typeof(bool));
        table.Columns.Add("fldHasSampleNumbersDefined", typeof(bool));
        table.Columns.Add("fldIsCancelled", typeof(bool));
        table.Columns.Add("fldIsAsAvailable", typeof(bool));
        table.Columns.Add("fldStoreRatings", typeof(bool));
        table.Columns.Add("fldSchemeVersionDate", typeof(DateTime));
        return table;
    }

    private static DataTable ParticipantTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        return table;
    }

    [Fact]
    public async Task GetMonthlyDistributionSchemeAsync_UnknownScheme_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var dataSet = new DataSet();
        dataSet.Tables.Add(SchemeTable());
        dataSet.Tables.Add(ParticipantTable());
        connection.RespondToQuery(GetSchemeSql, dataSet);

        Assert.Null(await repository.GetMonthlyDistributionSchemeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetMonthlyDistributionSchemeAsync_AggregatesParticipantsOntoScheme()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();

        var schemes = SchemeTable();
        schemes.Rows.Add(
            schemeId, Guid.NewGuid(), Guid.NewGuid(), "PT0060", "Mastitis bovine", "17152", "A", "BA",
            new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 4, 5), new DateTime(2026, 4, 10),
            "Some comments", false, false, false, false, false, new DateTime(2026, 1, 1));

        var participants = ParticipantTable();
        participants.Rows.Add(schemeId, 4);
        participants.Rows.Add(schemeId, 5);

        var dataSet = new DataSet();
        dataSet.Tables.Add(schemes);
        dataSet.Tables.Add(participants);
        connection.RespondToQuery(GetSchemeSql, dataSet);

        var scheme = await repository.GetMonthlyDistributionSchemeAsync(schemeId);

        Assert.NotNull(scheme);
        Assert.Equal(schemeId, scheme!.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA", scheme.DistributionReferenceFull);
        Assert.Equal(2, scheme.ParticipantCount);
        Assert.Equal(9, scheme.TotalSetsOfSamplesRequired);
    }

    // spgIntendedResults returns nine result sets; set 4 (Sample Result Items) is consumed and
    // discarded because it belongs to the Intended Results screen, not Sample Numbers.
    private static DataSet IntendedResultsDataSet(Guid schemeId, Guid sampleId, Guid sampleTestId, Guid testId, bool includeHeaderRow = true)
    {
        var header = new DataTable("Header");
        header.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        header.Columns.Add("fldComments", typeof(string));
        header.Columns.Add("fldHasIntendedResults", typeof(bool));
        header.Columns.Add("fldHasSampleNumbersDefined", typeof(bool));
        header.Columns.Add("fldTestConsultantTabulationId", typeof(Guid));
        header.Columns.Add("fldStoreRatings", typeof(bool));
        header.Columns.Add("fldRequiresAssessment", typeof(bool));
        header.Columns.Add("fldIsReadyForAssessor", typeof(DateTime));
        if (includeHeaderRow)
        {
            header.Rows.Add(schemeId, "Some comments", false, false, Guid.NewGuid(), true, true, DBNull.Value);
        }

        var samples = new DataTable("Samples");
        samples.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        samples.Columns.Add("fldSampleId", typeof(Guid));
        samples.Columns.Add("fldSampleNumber", typeof(string));
        samples.Columns.Add("fldInternalDescription", typeof(string));
        samples.Columns.Add("fldExternalDescription", typeof(string));
        samples.Columns.Add("fldTabulationDescription", typeof(string));
        samples.Columns.Add("fldTestConsultantsDescription", typeof(string));
        samples.Rows.Add(schemeId, sampleId, "S1", "Internal", "External", "Tabulation", "TC");

        var sampleTests = new DataTable("SampleTests");
        sampleTests.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        sampleTests.Columns.Add("fldSampleId", typeof(Guid));
        sampleTests.Columns.Add("fldSampleTestId", typeof(Guid));
        sampleTests.Columns.Add("fldTestId", typeof(Guid));
        sampleTests.Columns.Add("fldIsApplicable", typeof(bool));
        sampleTests.Rows.Add(schemeId, sampleId, sampleTestId, testId, true);

        var sampleResultItems = new DataTable("SampleResultItems");
        sampleResultItems.Columns.Add("fldSampleResultItemId", typeof(Guid));
        sampleResultItems.Rows.Add(Guid.NewGuid());

        var tests = new DataTable("Tests");
        tests.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        tests.Columns.Add("fldTestId", typeof(Guid));
        tests.Columns.Add("fldTestTypeId", typeof(Guid));
        tests.Columns.Add("TestName", typeof(string));
        tests.Columns.Add("fldOrder", typeof(int));
        tests.Rows.Add(schemeId, testId, Guid.NewGuid(), "Somatic cell count", 1);

        var methodItems = new DataTable("MethodItems");
        methodItems.Columns.Add("fldTestMethodItemId", typeof(Guid));
        methodItems.Columns.Add("fldTestId", typeof(Guid));
        methodItems.Columns.Add("TestMethodItemTypeName", typeof(string));
        methodItems.Columns.Add("fldOrder", typeof(int));
        methodItems.Rows.Add(Guid.NewGuid(), testId, "Method A", 1);

        var resultItems = new DataTable("ResultItems");
        resultItems.Columns.Add("fldTestResultItemId", typeof(Guid));
        resultItems.Columns.Add("fldTestId", typeof(Guid));
        resultItems.Columns.Add("TestResultItemTypeName", typeof(string));
        resultItems.Columns.Add("fldOrder", typeof(int));
        resultItems.Rows.Add(Guid.NewGuid(), testId, "Result A", 1);

        var categoryItemId = Guid.NewGuid();
        var categoryItems = new DataTable("CategoryItems");
        categoryItems.Columns.Add("fldCategoryItemId", typeof(Guid));
        categoryItems.Columns.Add("fldTestId", typeof(Guid));
        categoryItems.Columns.Add("fldOrder", typeof(int));
        categoryItems.Columns.Add("fldName", typeof(string));
        categoryItems.Rows.Add(categoryItemId, testId, 1, "Appearance");

        var criterionItems = new DataTable("CriterionItems");
        criterionItems.Columns.Add("fldCriterionItemId", typeof(Guid));
        criterionItems.Columns.Add("fldCategoryItemId", typeof(Guid));
        criterionItems.Columns.Add("fldTestId", typeof(Guid));
        criterionItems.Columns.Add("fldOrder", typeof(int));
        criterionItems.Columns.Add("fldName", typeof(string));
        criterionItems.Rows.Add(Guid.NewGuid(), categoryItemId, testId, 1, "Colour");

        var dataSet = new DataSet();
        dataSet.Tables.AddRange([header, samples, sampleTests, sampleResultItems, tests, methodItems, resultItems, categoryItems, criterionItems]);
        return dataSet;
    }

    [Fact]
    public async Task GetIntendedResultsAsync_NoHeaderRow_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetIntendedResultsSql, IntendedResultsDataSet(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), includeHeaderRow: false));

        Assert.Null(await repository.GetIntendedResultsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetIntendedResultsAsync_ReadsEveryResultSetInOrder()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var sampleTestId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        connection.RespondToQuery(GetIntendedResultsSql, IntendedResultsDataSet(schemeId, sampleId, sampleTestId, testId));

        var data = await repository.GetIntendedResultsAsync(schemeId);

        Assert.NotNull(data);
        Assert.Equal(schemeId, data!.Header.MonthlyDistributionSchemeId);
        Assert.Equal("Some comments", data.Header.Comments);
        Assert.True(data.Header.RequiresAssessment);
        Assert.Null(data.Header.IsReadyForAssessor);
        Assert.Equal(sampleId, Assert.Single(data.Samples).SampleId);
        Assert.Equal(sampleTestId, Assert.Single(data.SampleTests).SampleTestId);
        Assert.Equal("Somatic cell count", Assert.Single(data.Tests).TestName);
        Assert.Equal("Method A", Assert.Single(data.TestMethodItems).Name);
        Assert.Equal("Result A", Assert.Single(data.TestResultItems).Name);
        Assert.Equal("Appearance", Assert.Single(data.CategoryItems).Name);
        Assert.Equal("Colour", Assert.Single(data.CriterionItems).Name);
    }

    // Result set 4 (Sample Result Items) used to be consumed and discarded; it is now mapped onto
    // IntendedResultsData.SampleResultItems for the Intended Results screen.
    [Fact]
    public async Task GetIntendedResultsAsync_MapsSampleResultItemsFromTheFourthResultSet()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var sampleTestId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var testResultItemId = Guid.NewGuid();
        var sampleResultItemId = Guid.NewGuid();

        var dataSet = IntendedResultsDataSet(schemeId, sampleId, sampleTestId, testId);
        var sampleResultItems = dataSet.Tables["SampleResultItems"]!;
        sampleResultItems.Columns.Add("fldSampleId", typeof(Guid));
        sampleResultItems.Columns.Add("fldSampleTestId", typeof(Guid));
        sampleResultItems.Columns.Add("fldTestResultItemId", typeof(Guid));
        sampleResultItems.Columns.Add("fldIntendedResult", typeof(string));
        sampleResultItems.Columns.Add("fldIntendedResult2", typeof(string));
        sampleResultItems.Rows[0]["fldSampleResultItemId"] = sampleResultItemId;
        sampleResultItems.Rows[0]["fldSampleId"] = sampleId;
        sampleResultItems.Rows[0]["fldSampleTestId"] = sampleTestId;
        sampleResultItems.Rows[0]["fldTestResultItemId"] = testResultItemId;
        sampleResultItems.Rows[0]["fldIntendedResult"] = "12";
        sampleResultItems.Rows[0]["fldIntendedResult2"] = "20";
        connection.RespondToQuery(GetIntendedResultsSql, dataSet);

        var data = await repository.GetIntendedResultsAsync(schemeId);

        var sampleResultItem = Assert.Single(data!.SampleResultItems);
        Assert.Equal(sampleResultItemId, sampleResultItem.SampleResultItemId);
        Assert.Equal(sampleId, sampleResultItem.SampleId);
        Assert.Equal(sampleTestId, sampleResultItem.SampleTestId);
        Assert.Equal(testResultItemId, sampleResultItem.TestResultItemId);
        Assert.Equal("12", sampleResultItem.IntendedResult);
        Assert.Equal("20", sampleResultItem.IntendedResult2);
    }

    private const string UpdateIntendedResultsSql = """
        EXEC dbo.spuIntendedResults
            @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
            @Comments = @Comments,
            @HasIntendedResults = @HasIntendedResults,
            @HasSampleNumbersDefined = @HasSampleNumbersDefined,
            @TestConsultantTabulationId = @TestConsultantTabulationId
        """;

    private const string UpdateSampleSql = """
        EXEC dbo.spuSample
            @SampleId = @SampleId,
            @DistributionSchemeId = @DistributionSchemeId,
            @SampleNumber = @SampleNumber,
            @InternalDescription = @InternalDescription,
            @ExternalDescription = @ExternalDescription,
            @TabulationDescription = @TabulationDescription,
            @TestConsultantsDescription = @TestConsultantsDescription
        """;

    private const string UpdateSampleTestSql = """
        EXEC dbo.spuSampleTest
            @SampleTestId = @SampleTestId,
            @SampleId = @SampleId,
            @TestId = @TestId,
            @IsApplicable = @IsApplicable
        """;

    private const string SetupSampleReceiptSql = "EXEC dbo.spiSetupSampleReceipt @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";

    private const string AuditSampleSql = """
        EXEC dbo.spiAuditSample
            @SampleId = @SampleId,
            @AuditWho = @AuditWho,
            @AuditWhy = @AuditWhy,
            @TabulationDescription = @TabulationDescription,
            @TabulationDescriptionUpdate = 1
        """;

    [Fact]
    public async Task UpdateSampleNumbersAsync_WritesHeaderSamplesAndSampleTestsInOneTransaction()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(UpdateSampleSql, 1);
        connection.RespondToNonQuery(UpdateSampleTestSql, 1);

        var header = new PTL.Core.Distribution.IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = Guid.NewGuid() };
        var samples = new List<PTL.Core.Distribution.DistributionSampleEntity>
        {
            new() { SampleId = Guid.NewGuid() },
            new() { SampleId = Guid.NewGuid() },
        };
        var sampleTests = new List<PTL.Core.Distribution.DistributionSampleTestEntity> { new() { SampleTestId = Guid.NewGuid() } };

        await repository.UpdateSampleNumbersAsync(new PTL.Core.Distribution.SampleNumbersWrite(
            header, samples, sampleTests, [], "tester", string.Empty, SetupSampleReceipt: false));

        Assert.Equal(1, connection.ExecutedCommands.Count(c => c.CommandText == UpdateIntendedResultsSql));
        Assert.Equal(2, connection.ExecutedCommands.Count(c => c.CommandText == UpdateSampleSql));
        Assert.Equal(1, connection.ExecutedCommands.Count(c => c.CommandText == UpdateSampleTestSql));
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == SetupSampleReceiptSql);
        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task UpdateSampleNumbersAsync_SetupSampleReceipt_RunsTheAssessmentSetupProcedure()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(SetupSampleReceiptSql, 1);

        var header = new PTL.Core.Distribution.IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = Guid.NewGuid() };

        await repository.UpdateSampleNumbersAsync(new PTL.Core.Distribution.SampleNumbersWrite(
            header, [], [], [], "tester", string.Empty, SetupSampleReceipt: true));

        Assert.Equal(1, connection.ExecutedCommands.Count(c => c.CommandText == SetupSampleReceiptSql));
        Assert.True(connection.LastTransaction!.Committed);
    }

    // spiAuditSample copies the row's CURRENT Tabulation Description, so it has to run before the
    // spuSample that overwrites it (legacy Sample.Update: Audit(cn) then spuSample).
    [Fact]
    public async Task UpdateSampleNumbersAsync_AuditedSample_WritesAuditBeforeTheSampleUpdate()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(AuditSampleSql, 1);
        connection.RespondToNonQuery(UpdateSampleSql, 1);

        var auditedId = Guid.NewGuid();
        var header = new PTL.Core.Distribution.IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = Guid.NewGuid() };
        var samples = new List<PTL.Core.Distribution.DistributionSampleEntity>
        {
            new() { SampleId = auditedId, TabulationDescription = "new value" },
            new() { SampleId = Guid.NewGuid() },
        };

        await repository.UpdateSampleNumbersAsync(new PTL.Core.Distribution.SampleNumbersWrite(
            header, samples, [], [auditedId], "tester", "because", SetupSampleReceipt: false));

        var texts = connection.ExecutedCommands.Select(c => c.CommandText).ToList();
        Assert.Equal(1, texts.Count(t => t == AuditSampleSql));
        Assert.True(texts.IndexOf(AuditSampleSql) < texts.IndexOf(UpdateSampleSql));
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task UpdateSampleNumbersAsync_NoAuditedSamples_DoesNotWriteAnAuditRow()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(UpdateSampleSql, 1);

        var header = new PTL.Core.Distribution.IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = Guid.NewGuid() };
        var samples = new List<PTL.Core.Distribution.DistributionSampleEntity> { new() { SampleId = Guid.NewGuid() } };

        await repository.UpdateSampleNumbersAsync(new PTL.Core.Distribution.SampleNumbersWrite(
            header, samples, [], [], "tester", string.Empty, SetupSampleReceipt: false));

        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == AuditSampleSql);
    }

    [Fact]
    public async Task UpdateSampleNumbersAsync_AuditedSample_PassesTheCapturedWhoAndWhy()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(AuditSampleSql, 1);
        connection.RespondToNonQuery(UpdateSampleSql, 1);

        var auditedId = Guid.NewGuid();
        var header = new PTL.Core.Distribution.IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = Guid.NewGuid() };
        var samples = new List<PTL.Core.Distribution.DistributionSampleEntity>
        {
            new() { SampleId = auditedId, TabulationDescription = "new value" },
        };

        await repository.UpdateSampleNumbersAsync(new PTL.Core.Distribution.SampleNumbersWrite(
            header, samples, [], [auditedId], "APHA\\tester", "Corrected a typo", SetupSampleReceipt: false));

        var audit = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSampleSql);
        Assert.Equal(auditedId, audit.ParameterValue("SampleId"));
        Assert.Equal("APHA\\tester", audit.ParameterValue("AuditWho"));
        Assert.Equal("Corrected a typo", audit.ParameterValue("AuditWhy"));
        Assert.Equal("new value", audit.ParameterValue("TabulationDescription"));
    }

    // Each audited sample gets its own audit row, and each one still precedes its own spuSample.
    [Fact]
    public async Task UpdateSampleNumbersAsync_SeveralAuditedSamples_InterleavesAuditBeforeEachUpdate()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(AuditSampleSql, 1);
        connection.RespondToNonQuery(UpdateSampleSql, 1);

        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var header = new PTL.Core.Distribution.IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = Guid.NewGuid() };
        var samples = new List<PTL.Core.Distribution.DistributionSampleEntity>
        {
            new() { SampleId = firstId },
            new() { SampleId = secondId },
        };

        await repository.UpdateSampleNumbersAsync(new PTL.Core.Distribution.SampleNumbersWrite(
            header, samples, [], [firstId, secondId], "tester", "Because", SetupSampleReceipt: false));

        var texts = connection.ExecutedCommands
            .Select(c => c.CommandText)
            .Where(t => t == AuditSampleSql || t == UpdateSampleSql)
            .ToList();

        Assert.Equal([AuditSampleSql, UpdateSampleSql, AuditSampleSql, UpdateSampleSql], texts);
        Assert.Equal(
            [firstId, secondId],
            connection.ExecutedCommands.Where(c => c.CommandText == AuditSampleSql).Select(c => c.ParameterValue("SampleId")));
    }

    // A sample that is being written but was not flagged as audited must not get an audit row,
    // even when another sample in the same save was.
    [Fact]
    public async Task UpdateSampleNumbersAsync_MixedSamples_OnlyAuditsTheFlaggedOnes()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(AuditSampleSql, 1);
        connection.RespondToNonQuery(UpdateSampleSql, 1);

        var auditedId = Guid.NewGuid();
        var header = new PTL.Core.Distribution.IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = Guid.NewGuid() };
        var samples = new List<PTL.Core.Distribution.DistributionSampleEntity>
        {
            new() { SampleId = Guid.NewGuid() },
            new() { SampleId = auditedId },
            new() { SampleId = Guid.NewGuid() },
        };

        await repository.UpdateSampleNumbersAsync(new PTL.Core.Distribution.SampleNumbersWrite(
            header, samples, [], [auditedId], "tester", "Because", SetupSampleReceipt: false));

        var audit = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSampleSql);
        Assert.Equal(auditedId, audit.ParameterValue("SampleId"));
        Assert.Equal(3, connection.ExecutedCommands.Count(c => c.CommandText == UpdateSampleSql));
    }

    // An audited id with no matching sample row cannot produce an orphan audit row.
    [Fact]
    public async Task UpdateSampleNumbersAsync_AuditedIdWithNoSampleRow_WritesNoAudit()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(UpdateSampleSql, 1);

        var header = new PTL.Core.Distribution.IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = Guid.NewGuid() };

        await repository.UpdateSampleNumbersAsync(new PTL.Core.Distribution.SampleNumbersWrite(
            header, [], [], [Guid.NewGuid()], "tester", "Because", SetupSampleReceipt: false));

        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == AuditSampleSql);
        Assert.True(connection.LastTransaction!.Committed);
    }

    private const string AuditTestMethodItemSql = """
        EXEC dbo.spiAuditDistributionTestMethodItem
            @DistributionTestMethodItemId = @TestMethodItemId,
            @AuditWho = @AuditWho,
            @AuditWhy = @AuditWhy,
            @IntendedResult = @IntendedResult,
            @IntendedResultUpdate = 1
        """;

    private const string UpdateTestMethodItemSql = """
        EXEC dbo.spuDistributionTestMethodItem
            @DistributionTestMethodItemId = @TestMethodItemId,
            @TestId = @TestId,
            @TestMethodItemTypeId = @TestMethodItemTypeId,
            @IntendedResult = @IntendedResult,
            @IsApplicable = @IsApplicable,
            @Order = @Order
        """;

    private const string UpdateTestResultItemSql = """
        EXEC dbo.spuDistributionTestResultItem
            @DistributionTestResultItemId = @TestResultItemId,
            @TestId = @TestId,
            @TestResultItemTypeId = @TestResultItemTypeId,
            @Order = @Order,
            @IsRange = @IsRange
        """;

    private const string AuditSampleResultItemSql = """
        EXEC dbo.spiAuditSampleResultItem
            @SampleResultItemId = @SampleResultItemId,
            @AuditWho = @AuditWho,
            @AuditWhy = @AuditWhy,
            @IntendedResult = @IntendedResult,
            @IntendedResultUpdate = @IntendedResultUpdate,
            @IntendedResult2 = @IntendedResult2,
            @IntendedResult2Update = @IntendedResult2Update
        """;

    private const string UpdateSampleResultItemSql = """
        EXEC dbo.spuSampleResultItem
            @SampleResultItemId = @SampleResultItemId,
            @SampleTestId = @SampleTestId,
            @TestResultItemId = @TestResultItemId,
            @IntendedResult = @IntendedResult,
            @IntendedResult2 = @IntendedResult2
        """;

    private static PTL.Core.Distribution.IntendedResultsHeaderEntity IntendedResultsHeader() =>
        new() { MonthlyDistributionSchemeId = Guid.NewGuid() };

    [Fact]
    public async Task UpdateIntendedResultsAsync_WritesHeaderMethodItemsResultItemsAndSampleResultItemsInOneTransaction()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(UpdateTestMethodItemSql, 1);
        connection.RespondToNonQuery(UpdateTestResultItemSql, 1);
        connection.RespondToNonQuery(UpdateSampleResultItemSql, 1);

        var methodItems = new List<PTL.Core.Distribution.DistributionTestMethodItemEntity> { new() { TestMethodItemId = Guid.NewGuid() } };
        var resultItems = new List<PTL.Core.Distribution.DistributionTestResultItemEntity> { new() { TestResultItemId = Guid.NewGuid() } };
        var sampleResultItems = new List<PTL.Core.Distribution.DistributionSampleResultItemEntity>
        {
            new() { SampleResultItemId = Guid.NewGuid() },
            new() { SampleResultItemId = Guid.NewGuid() },
        };

        await repository.UpdateIntendedResultsAsync(new PTL.Core.Distribution.IntendedResultsWrite(
            IntendedResultsHeader(), methodItems, [], resultItems, sampleResultItems, [], "tester", string.Empty));

        Assert.Equal(1, connection.ExecutedCommands.Count(c => c.CommandText == UpdateIntendedResultsSql));
        Assert.Equal(1, connection.ExecutedCommands.Count(c => c.CommandText == UpdateTestMethodItemSql));
        Assert.Equal(1, connection.ExecutedCommands.Count(c => c.CommandText == UpdateTestResultItemSql));
        Assert.Equal(2, connection.ExecutedCommands.Count(c => c.CommandText == UpdateSampleResultItemSql));
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == AuditTestMethodItemSql);
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == AuditSampleResultItemSql);
        Assert.True(connection.LastTransaction!.Committed);
    }

    // spiAuditDistributionTestMethodItem copies the row's CURRENT IntendedResult, so it must run
    // before the spuDistributionTestMethodItem that overwrites it.
    [Fact]
    public async Task UpdateIntendedResultsAsync_AuditedMethodItem_WritesAuditBeforeTheUpdate()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(AuditTestMethodItemSql, 1);
        connection.RespondToNonQuery(UpdateTestMethodItemSql, 1);

        var auditedId = Guid.NewGuid();
        var methodItems = new List<PTL.Core.Distribution.DistributionTestMethodItemEntity>
        {
            new() { TestMethodItemId = auditedId, IntendedResult = "new value" },
        };

        await repository.UpdateIntendedResultsAsync(new PTL.Core.Distribution.IntendedResultsWrite(
            IntendedResultsHeader(), methodItems, [auditedId], [], [], [], "APHA\\tester", "Corrected a typo"));

        var texts = connection.ExecutedCommands.Select(c => c.CommandText).ToList();
        Assert.Equal(1, texts.Count(t => t == AuditTestMethodItemSql));
        Assert.True(texts.IndexOf(AuditTestMethodItemSql) < texts.IndexOf(UpdateTestMethodItemSql));

        var audit = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditTestMethodItemSql);
        Assert.Equal(auditedId, audit.ParameterValue("TestMethodItemId"));
        Assert.Equal("APHA\\tester", audit.ParameterValue("AuditWho"));
        Assert.Equal("Corrected a typo", audit.ParameterValue("AuditWhy"));
        Assert.Equal("new value", audit.ParameterValue("IntendedResult"));
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task UpdateIntendedResultsAsync_NoAuditedMethodItems_WritesNoAuditRow()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(UpdateTestMethodItemSql, 1);

        var methodItems = new List<PTL.Core.Distribution.DistributionTestMethodItemEntity> { new() { TestMethodItemId = Guid.NewGuid() } };

        await repository.UpdateIntendedResultsAsync(new PTL.Core.Distribution.IntendedResultsWrite(
            IntendedResultsHeader(), methodItems, [], [], [], [], "tester", string.Empty));

        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == AuditTestMethodItemSql);
    }

    // TestResultItem (IsRange) is never audited - legacy's TestResultItem is a plain BusinessBase.
    [Fact]
    public async Task UpdateIntendedResultsAsync_TestResultItems_AreNeverAudited()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(UpdateTestResultItemSql, 1);

        var resultItemId = Guid.NewGuid();
        var resultItems = new List<PTL.Core.Distribution.DistributionTestResultItemEntity> { new() { TestResultItemId = resultItemId, IsRange = true } };

        await repository.UpdateIntendedResultsAsync(new PTL.Core.Distribution.IntendedResultsWrite(
            IntendedResultsHeader(), [], [], resultItems, [], [], "tester", string.Empty));

        var update = Assert.Single(connection.ExecutedCommands, c => c.CommandText == UpdateTestResultItemSql);
        Assert.Equal(resultItemId, update.ParameterValue("TestResultItemId"));
        Assert.True((bool)update.ParameterValue("IsRange")!);
    }

    // spiAuditSampleResultItem copies the row's CURRENT values, so it must run before
    // spuSampleResultItem overwrites them.
    [Fact]
    public async Task UpdateIntendedResultsAsync_AuditedSampleResultItem_WritesAuditBeforeTheUpdate()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(AuditSampleResultItemSql, 1);
        connection.RespondToNonQuery(UpdateSampleResultItemSql, 1);

        var auditedId = Guid.NewGuid();
        var sampleResultItems = new List<PTL.Core.Distribution.DistributionSampleResultItemEntity>
        {
            new() { SampleResultItemId = auditedId, IntendedResult = "10", IntendedResult2 = "20" },
        };
        var auditedChanges = new List<PTL.Core.Distribution.AuditedSampleResultItemChange>
        {
            new(auditedId, IntendedResultChanged: true, IntendedResult2Changed: true),
        };

        await repository.UpdateIntendedResultsAsync(new PTL.Core.Distribution.IntendedResultsWrite(
            IntendedResultsHeader(), [], [], [], sampleResultItems, auditedChanges, "APHA\\tester", "Because"));

        var texts = connection.ExecutedCommands.Select(c => c.CommandText).ToList();
        Assert.Equal(1, texts.Count(t => t == AuditSampleResultItemSql));
        Assert.True(texts.IndexOf(AuditSampleResultItemSql) < texts.IndexOf(UpdateSampleResultItemSql));

        var audit = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSampleResultItemSql);
        Assert.Equal(auditedId, audit.ParameterValue("SampleResultItemId"));
        Assert.Equal("10", audit.ParameterValue("IntendedResult"));
        Assert.Equal("20", audit.ParameterValue("IntendedResult2"));
        Assert.True((bool)audit.ParameterValue("IntendedResultUpdate")!);
        Assert.True((bool)audit.ParameterValue("IntendedResult2Update")!);
        Assert.True(connection.LastTransaction!.Committed);
    }

    // Each SampleResultItem has two independently auditable values - only the one(s) that
    // actually changed should have their Update flag set.
    [Fact]
    public async Task UpdateIntendedResultsAsync_AuditedSampleResultItem_PassesIndependentUpdateFlagsPerValue()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(AuditSampleResultItemSql, 1);
        connection.RespondToNonQuery(UpdateSampleResultItemSql, 1);

        var auditedId = Guid.NewGuid();
        var sampleResultItems = new List<PTL.Core.Distribution.DistributionSampleResultItemEntity>
        {
            new() { SampleResultItemId = auditedId, IntendedResult = "10", IntendedResult2 = "unchanged" },
        };
        var auditedChanges = new List<PTL.Core.Distribution.AuditedSampleResultItemChange>
        {
            new(auditedId, IntendedResultChanged: true, IntendedResult2Changed: false),
        };

        await repository.UpdateIntendedResultsAsync(new PTL.Core.Distribution.IntendedResultsWrite(
            IntendedResultsHeader(), [], [], [], sampleResultItems, auditedChanges, "tester", "Because"));

        var audit = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSampleResultItemSql);
        Assert.True((bool)audit.ParameterValue("IntendedResultUpdate")!);
        Assert.False((bool)audit.ParameterValue("IntendedResult2Update")!);
    }

    [Fact]
    public async Task UpdateIntendedResultsAsync_NoAuditedSampleResultItems_WritesNoAuditRow()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(UpdateSampleResultItemSql, 1);

        var sampleResultItems = new List<PTL.Core.Distribution.DistributionSampleResultItemEntity> { new() { SampleResultItemId = Guid.NewGuid() } };

        await repository.UpdateIntendedResultsAsync(new PTL.Core.Distribution.IntendedResultsWrite(
            IntendedResultsHeader(), [], [], [], sampleResultItems, [], "tester", string.Empty));

        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == AuditSampleResultItemSql);
        Assert.True(connection.LastTransaction!.Committed);
    }

    // A SampleResultItem being written but not flagged audited must not get an audit row, even
    // when another item in the same save was.
    [Fact]
    public async Task UpdateIntendedResultsAsync_MixedSampleResultItems_OnlyAuditsTheFlaggedOnes()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateIntendedResultsSql, 1);
        connection.RespondToNonQuery(AuditSampleResultItemSql, 1);
        connection.RespondToNonQuery(UpdateSampleResultItemSql, 1);

        var auditedId = Guid.NewGuid();
        var sampleResultItems = new List<PTL.Core.Distribution.DistributionSampleResultItemEntity>
        {
            new() { SampleResultItemId = Guid.NewGuid() },
            new() { SampleResultItemId = auditedId },
            new() { SampleResultItemId = Guid.NewGuid() },
        };
        var auditedChanges = new List<PTL.Core.Distribution.AuditedSampleResultItemChange> { new(auditedId, true, false) };

        await repository.UpdateIntendedResultsAsync(new PTL.Core.Distribution.IntendedResultsWrite(
            IntendedResultsHeader(), [], [], [], sampleResultItems, auditedChanges, "tester", "Because"));

        var audit = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSampleResultItemSql);
        Assert.Equal(auditedId, audit.ParameterValue("SampleResultItemId"));
        Assert.Equal(3, connection.ExecutedCommands.Count(c => c.CommandText == UpdateSampleResultItemSql));
    }
}
