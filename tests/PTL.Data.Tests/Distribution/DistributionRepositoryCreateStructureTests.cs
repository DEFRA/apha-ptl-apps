using System.Data;
using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// CreateDistributionStructureAsync - the first-time "Define Sample Numbers" write, ported from
// legacy IntendedResultsSet.Save() on a new object. Everything happens in one Serializable
// transaction because spgNextSampleNumber advances a counter on tblSchedule.
public class DistributionRepositoryCreateStructureTests
{
    public DistributionRepositoryCreateStructureTests() => DapperColumnMappings.Register();

    private static readonly Guid DistributionSchemeId = Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222");
    private static readonly Guid ScheduleId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly DateTime SampleNumberDate = new(2026, 4, 1, 0, 0, 2, DateTimeKind.Unspecified);

    private const string GetIntendedResultsSql = "EXEC dbo.spgIntendedResults @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";

    private const string InsertIntendedResultsSql = """
        EXEC dbo.spiIntendedResults
            @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
            @Comments = @Comments,
            @HasIntendedResults = 0,
            @HasSampleNumbersDefined = 0,
            @TestConsultantTabulationId = @TestConsultantTabulationId,
            @SchemeVersionDate = @SchemeVersionDate,
            @DateOfReceipt = @DateOfReceipt,
            @StorageConditions = @StorageConditions,
            @ConditionOnReceipt = @ConditionOnReceipt,
            @Instructions = @Instructions,
            @StandardTabulationText = @StandardTabulationText
        """;

    private const string InsertTestSql =
        "EXEC dbo.spiDistributionTest @DistributionTestId = @TestId, @TestTypeId = @TestTypeId, @DistributionSchemeId = @DistributionSchemeId, @Order = @Order";

    private const string InsertMethodItemSql = """
        EXEC dbo.spiDistributionTestMethodItem
            @DistributionTestMethodItemId = @TestMethodItemId,
            @TestId = @TestId,
            @TestMethodItemTypeId = @TestMethodItemTypeId,
            @IntendedResult = @IntendedResult,
            @Order = @Order,
            @IsApplicable = @IsApplicable
        """;

    private const string InsertResultItemSql = """
        EXEC dbo.spiDistributionTestResultItem
            @DistributionTestResultItemId = @TestResultItemId,
            @TestId = @TestId,
            @TestResultItemTypeId = @TestResultItemTypeId,
            @Order = @Order,
            @IsRange = @IsRange
        """;

    private const string InsertCategoryItemSql =
        "EXEC dbo.spiDistributionCategoryItem @CategoryItemId = @CategoryItemId, @TestId = @TestId, @CategoryItemTypeId = @CategoryItemTypeId, @Order = @Order";

    private const string InsertCriterionItemSql =
        "EXEC dbo.spiDistributionCriterionItem @CriterionItemId = @CriterionItemId, @CategoryItemId = @CategoryItemId, @CriterionItemTypeId = @CriterionItemTypeId, @Order = @Order";

    private const string InsertTabulationSql = """
        EXEC dbo.spiDistributionTabulation
            @DistributionTabulationId = @TabulationId,
            @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
            @Name = @Name,
            @IntendedResultsOnly = @IntendedResultsOnly,
            @SchemeVersionDate = @SchemeVersionDate,
            @SingleParticipantTabulation = @SingleParticipantTabulation,
            @ShowRatings = @ShowRatings,
            @AvailableToParticipants = @AvailableToParticipants,
            @AvailableToViewers = @AvailableToViewers
        """;

    private const string InsertTabulationMethodItemSql =
        "EXEC dbo.spiDistributionTabulationTestMethodItem @DistributionTabulationTestMethodItemId = @LinkId, @TabulationId = @TabulationId, @TestMethodItemId = @ItemId";

    private const string InsertTabulationResultItemSql =
        "EXEC dbo.spiDistributionTabulationTestResultItem @DistributionTabulationTestResultItemId = @LinkId, @TabulationId = @TabulationId, @TestResultItemId = @ItemId";

    private const string NextSampleNumberSql =
        "EXEC dbo.spgNextSampleNumber @ScheduleId = @ScheduleId, @DistributionDate = @DistributionDate";

    private const string InsertSampleSql = """
        EXEC dbo.spiSample
            @SampleId = @SampleId,
            @DistributionSchemeId = @DistributionSchemeId,
            @SampleNumber = @SampleNumber,
            @InternalDescription = '',
            @ExternalDescription = '',
            @TabulationDescription = '',
            @TestConsultantsDescription = ''
        """;

    private const string InsertSampleTestSql =
        "EXEC dbo.spiSampleTest @SampleTestId = @SampleTestId, @SampleId = @SampleId, @TestId = @TestId, @IsApplicable = @IsApplicable";

    private const string InsertSampleResultItemSql = """
        EXEC dbo.spiSampleResultItem
            @SampleResultItemId = @SampleResultItemId,
            @SampleTestId = @SampleTestId,
            @TestResultItemId = @TestResultItemId,
            @IntendedResult = '',
            @IntendedResult2 = ''
        """;

    // The re-check only reads the header and the samples, so two result sets are enough.
    private static DataSet ReCheckDataSet(int sampleCount)
    {
        var header = new DataTable("Header");
        header.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        header.Rows.Add(DistributionSchemeId);

        var samples = new DataTable("Samples");
        samples.Columns.Add("fldSampleId", typeof(Guid));
        samples.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        for (var i = 0; i < sampleCount; i++)
        {
            samples.Rows.Add(Guid.NewGuid(), DistributionSchemeId);
        }

        var dataSet = new DataSet();
        dataSet.Tables.AddRange([header, samples]);
        return dataSet;
    }

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository(int existingSampleCount = 0)
    {
        var connection = new FakeDbConnection();
        connection.RespondToQuery(GetIntendedResultsSql, ReCheckDataSet(existingSampleCount));
        foreach (var sql in new[]
        {
            InsertIntendedResultsSql, InsertTestSql, InsertMethodItemSql, InsertResultItemSql, InsertCategoryItemSql,
            InsertCriterionItemSql, InsertTabulationSql, InsertTabulationMethodItemSql, InsertTabulationResultItemSql,
            InsertSampleSql, InsertSampleTestSql, InsertSampleResultItemSql,
        })
        {
            connection.RespondToNonQuery(sql, 1);
        }

        connection.RespondToScalar(NextSampleNumberSql, 7, 8);
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static readonly Guid TestId = Guid.Parse("11111111-2222-2222-2222-222222222222");
    private static readonly Guid ResultItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MethodItemId = Guid.Parse("33333333-2222-2222-2222-222222222222");
    private static readonly Guid CategoryItemId = Guid.Parse("44444444-2222-2222-2222-222222222222");
    private static readonly Guid TabulationId = Guid.Parse("55555555-2222-2222-2222-222222222222");

    private static DistributionStructure Structure(int sampleCount = 2)
    {
        var samples = new List<NewDistributionSample>();
        for (var i = 0; i < sampleCount; i++)
        {
            var sampleId = Guid.NewGuid();
            var sampleTestId = Guid.NewGuid();
            samples.Add(new NewDistributionSample
            {
                SampleId = sampleId,
                SampleTests =
                [
                    new NewDistributionSampleTest(sampleTestId, sampleId, TestId, true,
                        [new NewDistributionSampleResultItem(Guid.NewGuid(), sampleTestId, ResultItemId)]),
                ],
            });
        }

        return new DistributionStructure(
            [
                new NewDistributionTest(TestId, Guid.NewGuid(), 1,
                    [new NewDistributionTestResultItem(ResultItemId, TestId, Guid.NewGuid(), 1)],
                    [new NewDistributionTestMethodItem(MethodItemId, TestId, Guid.NewGuid(), 1)],
                    [
                        new NewDistributionCategoryItem(CategoryItemId, TestId, Guid.NewGuid(), 1,
                            [new NewDistributionCriterionItem(Guid.NewGuid(), CategoryItemId, Guid.NewGuid(), 1)]),
                    ]),
            ],
            [
                new NewDistributionTabulation(TabulationId, DistributionSchemeId, "Main tabulation", true,
                    new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Unspecified), true, true, true, true,
                    [new NewDistributionTabulationItem(Guid.NewGuid(), TabulationId, ResultItemId)],
                    [new NewDistributionTabulationItem(Guid.NewGuid(), TabulationId, MethodItemId)]),
            ],
            samples,
            TabulationId,
            new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Unspecified),
            DateOfReceipt: true,
            StorageConditions: true,
            ConditionOnReceipt: true,
            "Keep refrigerated",
            "Standard text");
    }

    private static Task<bool> CreateAsync(DistributionRepository repository, DistributionStructure structure) =>
        repository.CreateDistributionStructureAsync(DistributionSchemeId, ScheduleId, SampleNumberDate, structure);

    // Two users opening the page at once must not both build a structure.
    [Fact]
    public async Task CreateDistributionStructureAsync_SamplesAlreadyExist_RollsBackAndWritesNothing()
    {
        var (repository, connection) = CreateRepository(existingSampleCount: 1);

        var created = await CreateAsync(repository, Structure());

        Assert.False(created);
        Assert.Equal([GetIntendedResultsSql], connection.ExecutedCommands.Select(c => c.CommandText));
        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_NoExistingSamples_CommitsAndReportsSuccess()
    {
        var (repository, connection) = CreateRepository();

        var created = await CreateAsync(repository, Structure());

        Assert.True(created);
        Assert.True(connection.LastTransaction!.Committed);
        Assert.False(connection.LastTransaction.RolledBack);
    }

    // spgNextSampleNumber advances a counter on tblSchedule, so a rollback has to give the
    // numbers back - every command must be inside one Serializable transaction.
    [Fact]
    public async Task CreateDistributionStructureAsync_UsesOneSerializableTransactionForEveryCommand()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure());

        Assert.Equal(IsolationLevel.Serializable, connection.LastTransaction!.IsolationLevel);
        Assert.All(connection.ExecutedCommands, command => Assert.Same(connection.LastTransaction, command.Transaction));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_WritesEveryRowInLegacyOrder()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure(sampleCount: 1));

        Assert.Equal(
            [
                GetIntendedResultsSql,
                InsertIntendedResultsSql,
                InsertTestSql,
                InsertMethodItemSql,
                InsertResultItemSql,
                InsertCategoryItemSql,
                InsertCriterionItemSql,
                InsertTabulationSql,
                InsertTabulationMethodItemSql,
                InsertTabulationResultItemSql,
                NextSampleNumberSql,
                InsertSampleSql,
                InsertSampleTestSql,
                InsertSampleResultItemSql,
            ],
            connection.ExecutedCommands.Select(c => c.CommandText));
    }

    // Legacy Test.Insert writes Method Items before Result Items.
    [Fact]
    public async Task CreateDistributionStructureAsync_WritesMethodItemsBeforeResultItems()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure());

        var texts = connection.ExecutedCommands.Select(c => c.CommandText).ToList();
        Assert.True(texts.IndexOf(InsertMethodItemSql) < texts.IndexOf(InsertResultItemSql));
    }

    // The distribution stays a DRAFT until the user saves: both flags are written as literal 0.
    [Fact]
    public async Task CreateDistributionStructureAsync_WritesTheHeaderAsADraft()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure());

        var header = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertIntendedResultsSql);
        Assert.Contains("@HasIntendedResults = 0", header.CommandText, StringComparison.Ordinal);
        Assert.Contains("@HasSampleNumbersDefined = 0", header.CommandText, StringComparison.Ordinal);
        Assert.DoesNotContain("@HasIntendedResults = @HasIntendedResults", header.CommandText, StringComparison.Ordinal);
        Assert.DoesNotContain("@HasSampleNumbersDefined = @HasSampleNumbersDefined", header.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_PassesTheSchemeDerivedHeaderValues()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure());

        var header = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertIntendedResultsSql);
        Assert.Equal(DistributionSchemeId, header.ParameterValue("MonthlyDistributionSchemeId"));
        Assert.Equal(string.Empty, header.ParameterValue("Comments"));
        Assert.Equal(TabulationId, header.ParameterValue("TestConsultantTabulationId"));
        Assert.Equal(new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Unspecified), header.ParameterValue("SchemeVersionDate"));
        Assert.Equal(true, header.ParameterValue("DateOfReceipt"));
        Assert.Equal("Keep refrigerated", header.ParameterValue("Instructions"));
        Assert.Equal("Standard text", header.ParameterValue("StandardTabulationText"));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_AllocatesOneSampleNumberPerSampleFromTheSchemesSchedule()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure(sampleCount: 2));

        var allocations = connection.ExecutedCommands.Where(c => c.CommandText == NextSampleNumberSql).ToList();
        Assert.Equal(2, allocations.Count);
        Assert.All(allocations, c => Assert.Equal(ScheduleId, c.ParameterValue("ScheduleId")));
        Assert.All(allocations, c => Assert.Equal(SampleNumberDate, c.ParameterValue("DistributionDate")));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_FormatsTheAllocatedNumberOntoEachSample()
    {
        var (repository, connection) = CreateRepository();
        var structure = Structure(sampleCount: 2);

        await CreateAsync(repository, structure);

        Assert.Equal(["26/0007", "26/0008"], structure.Samples.Select(s => s.SampleNumber));
        Assert.Equal(
            ["26/0007", "26/0008"],
            connection.ExecutedCommands.Where(c => c.CommandText == InsertSampleSql).Select(c => c.ParameterValue("SampleNumber")));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_AllocatesTheSampleNumberBeforeInsertingTheSample()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure(sampleCount: 1));

        var texts = connection.ExecutedCommands.Select(c => c.CommandText).ToList();
        Assert.True(texts.IndexOf(NextSampleNumberSql) < texts.IndexOf(InsertSampleSql));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_WritesEverySampleTestAndItsResultItems()
    {
        var (repository, connection) = CreateRepository();
        var structure = Structure(sampleCount: 2);

        await CreateAsync(repository, structure);

        Assert.Equal(2, connection.ExecutedCommands.Count(c => c.CommandText == InsertSampleTestSql));
        Assert.Equal(2, connection.ExecutedCommands.Count(c => c.CommandText == InsertSampleResultItemSql));
        Assert.Equal(
            structure.Samples.SelectMany(s => s.SampleTests).Select(t => (object?)t.SampleTestId),
            connection.ExecutedCommands.Where(c => c.CommandText == InsertSampleTestSql).Select(c => c.ParameterValue("SampleTestId")));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_SendsEachSampleToTheDistributionBeingDefined()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure(sampleCount: 2));

        Assert.All(
            connection.ExecutedCommands.Where(c => c.CommandText == InsertSampleSql),
            c => Assert.Equal(DistributionSchemeId, c.ParameterValue("DistributionSchemeId")));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_SendsEachTestToTheDistributionBeingDefined()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure());

        var test = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTestSql);
        Assert.Equal(TestId, test.ParameterValue("TestId"));
        Assert.Equal(DistributionSchemeId, test.ParameterValue("DistributionSchemeId"));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_WritesTabulationLinksAgainstTheDistributionsOwnItems()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure());

        var methodLink = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTabulationMethodItemSql);
        var resultLink = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTabulationResultItemSql);
        Assert.Equal(TabulationId, methodLink.ParameterValue("TabulationId"));
        Assert.Equal(MethodItemId, methodLink.ParameterValue("ItemId"));
        Assert.Equal(ResultItemId, resultLink.ParameterValue("ItemId"));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_WritesTheTabulationBeforeItsLinkRows()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure());

        var texts = connection.ExecutedCommands.Select(c => c.CommandText).ToList();
        Assert.True(texts.IndexOf(InsertTabulationSql) < texts.IndexOf(InsertTabulationMethodItemSql));
        Assert.True(texts.IndexOf(InsertTabulationMethodItemSql) < texts.IndexOf(InsertTabulationResultItemSql));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_WritesCriterionItemsUnderTheirCategoryItem()
    {
        var (repository, connection) = CreateRepository();

        await CreateAsync(repository, Structure());

        var category = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertCategoryItemSql);
        var criterion = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertCriterionItemSql);
        Assert.Equal(CategoryItemId, category.ParameterValue("CategoryItemId"));
        Assert.Equal(CategoryItemId, criterion.ParameterValue("CategoryItemId"));
        var texts = connection.ExecutedCommands.Select(c => c.CommandText).ToList();
        Assert.True(texts.IndexOf(InsertCategoryItemSql) < texts.IndexOf(InsertCriterionItemSql));
    }

    [Fact]
    public async Task CreateDistributionStructureAsync_SchemeWithNoSamples_StillWritesTheStructureAndCommits()
    {
        var (repository, connection) = CreateRepository();

        var created = await CreateAsync(repository, Structure(sampleCount: 0));

        Assert.True(created);
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == NextSampleNumberSql);
        Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertTestSql);
        Assert.True(connection.LastTransaction!.Committed);
    }
}
