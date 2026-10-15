using Dapper;
using PTL.Core.Distribution;
using PTL.Data.Infrastructure;

namespace PTL.Data.Distribution;

public sealed partial class DistributionRepository(IDbConnectionFactory connectionFactory) : IDistributionRepository
{
    public async Task<IReadOnlyList<DistributionMonthSummaryEntity>> GetMonthlyDistributionSummariesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<DistributionMonthSummaryEntity>("EXEC dbo.spgaMonthlyDistributionInfo")).ToList();
    }

    public async Task<IReadOnlyList<DistributionMonthYearEntity>> GetDistributionYearsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<DistributionMonthYearEntity>("EXEC dbo.spgaDistributionYearInfo")).ToList();
    }

    public async Task<(Guid MonthlyDistributionId, IReadOnlyList<MonthlyDistributionSchemeEntity> Schemes)?> GetMonthlyDistributionSchedulesAsync(
        int yearId, int monthId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId",
            new { YearId = yearId, MonthId = monthId });

        var header = await results.ReadFirstOrDefaultAsync<MonthlyDistributionHeaderEntity>();
        var schemes = (await results.ReadAsync<MonthlyDistributionSchemeEntity>()).ToList();
        var participants = (await results.ReadAsync<MonthlyDistributionParticipantAggregateEntity>()).ToList();

        if (header is null || header.MonthlyDistributionId == Guid.Empty)
        {
            return null;
        }

        var participantsBySchemeId = participants
            .GroupBy(p => p.DistributionSchemeId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Total: g.Sum(p => p.NumberOfSetsRequired)));

        foreach (var scheme in schemes)
        {
            if (participantsBySchemeId.TryGetValue(scheme.MonthlyDistributionSchemeId, out var aggregate))
            {
                scheme.ParticipantCount = aggregate.Count;
                scheme.TotalSetsOfSamplesRequired = aggregate.Total;
            }
        }

        return (header.MonthlyDistributionId, schemes);
    }

    public async Task UpdateMonthlyDistributionSchemesAsync(IReadOnlyList<MonthlyDistributionSchemeEntity> schemes, CancellationToken cancellationToken = default)
    {
        if (schemes.Count == 0)
        {
            return;
        }

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

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

        foreach (var scheme in schemes)
        {
            await connection.ExecuteAsync(sql, scheme, transaction);
        }

        transaction.Commit();
    }

    public async Task<MonthlyDistributionSchemeEntity?> GetMonthlyDistributionSchemeAsync(
        Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistributionScheme @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId });

        var scheme = await results.ReadFirstOrDefaultAsync<MonthlyDistributionSchemeEntity>();
        if (scheme is null)
        {
            return null;
        }

        var participants = (await results.ReadAsync<MonthlyDistributionParticipantAggregateEntity>()).ToList();
        scheme.ParticipantCount = participants.Count;
        scheme.TotalSetsOfSamplesRequired = participants.Sum(p => p.NumberOfSetsRequired);
        return scheme;
    }

    public async Task<IntendedResultsData?> GetIntendedResultsAsync(
        Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgIntendedResults @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId });

        var header = await results.ReadFirstOrDefaultAsync<IntendedResultsHeaderEntity>();
        if (header is null)
        {
            return null;
        }

        var samples = (await results.ReadAsync<DistributionSampleEntity>()).ToList();
        var sampleTests = (await results.ReadAsync<DistributionSampleTestEntity>()).ToList();
        var sampleResultItems = (await results.ReadAsync<DistributionSampleResultItemEntity>()).ToList();
        var tests = (await results.ReadAsync<DistributionTestEntity>()).ToList();
        var methodItems = (await results.ReadAsync<DistributionTestMethodItemEntity>()).ToList();
        var resultItems = (await results.ReadAsync<DistributionTestResultItemEntity>()).ToList();
        var categoryItems = (await results.ReadAsync<DistributionCategoryItemEntity>()).ToList();
        var criterionItems = (await results.ReadAsync<DistributionCriterionItemEntity>()).ToList();

        return new IntendedResultsData(header, samples, sampleTests, tests, methodItems, resultItems, categoryItems, criterionItems, sampleResultItems);
    }

    public async Task UpdateSampleNumbersAsync(SampleNumbersWrite write, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            EXEC dbo.spuIntendedResults
                @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                @Comments = @Comments,
                @HasIntendedResults = @HasIntendedResults,
                @HasSampleNumbersDefined = @HasSampleNumbersDefined,
                @TestConsultantTabulationId = @TestConsultantTabulationId
            """,
            write.Header,
            transaction);

        const string auditSql = """
            EXEC dbo.spiAuditSample
                @SampleId = @SampleId,
                @AuditWho = @AuditWho,
                @AuditWhy = @AuditWhy,
                @TabulationDescription = @TabulationDescription,
                @TabulationDescriptionUpdate = 1
            """;

        const string sampleSql = """
            EXEC dbo.spuSample
                @SampleId = @SampleId,
                @DistributionSchemeId = @DistributionSchemeId,
                @SampleNumber = @SampleNumber,
                @InternalDescription = @InternalDescription,
                @ExternalDescription = @ExternalDescription,
                @TabulationDescription = @TabulationDescription,
                @TestConsultantsDescription = @TestConsultantsDescription
            """;

        var auditedSampleIds = write.AuditedSampleIds.ToHashSet();

        foreach (var sample in write.Samples)
        {
            // Must precede spuSample: the procedure copies the row's CURRENT value into the audit.
            if (auditedSampleIds.Contains(sample.SampleId))
            {
                await connection.ExecuteAsync(
                    auditSql,
                    new { sample.SampleId, write.AuditWho, write.AuditWhy, sample.TabulationDescription },
                    transaction);
            }

            await connection.ExecuteAsync(sampleSql, sample, transaction);
        }

        const string sampleTestSql = """
            EXEC dbo.spuSampleTest
                @SampleTestId = @SampleTestId,
                @SampleId = @SampleId,
                @TestId = @TestId,
                @IsApplicable = @IsApplicable
            """;

        foreach (var sampleTest in write.SampleTests)
        {
            await connection.ExecuteAsync(sampleTestSql, sampleTest, transaction);
        }

        if (write.SetupSampleReceipt)
        {
            await connection.ExecuteAsync(
                "EXEC dbo.spiSetupSampleReceipt @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
                new { write.Header.MonthlyDistributionSchemeId },
                transaction);
        }

        transaction.Commit();
    }

    public async Task<bool> CreateDistributionStructureAsync(
        Guid monthlyDistributionSchemeId,
        Guid scheduleId,
        DateTime sampleNumberDate,
        DistributionStructure distributionStructure,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);

        // Re-check inside the transaction: two users opening the page at once must not both build
        // a structure. spgIntendedResults is the existing read for this, so no new SQL is needed.
        bool alreadyCreated;
        using (var existing = await connection.QueryMultipleAsync(
            "EXEC dbo.spgIntendedResults @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId },
            transaction))
        {
            _ = await existing.ReadFirstOrDefaultAsync<IntendedResultsHeaderEntity>();
            alreadyCreated = (await existing.ReadAsync<DistributionSampleEntity>()).Any();
        }

        // Rolled back only once the grid reader is closed: spgIntendedResults returns further
        // unread result sets, and SqlClient rejects a rollback while a reader is still open.
        if (alreadyCreated)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(
            """
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
            """,
            new
            {
                MonthlyDistributionSchemeId = monthlyDistributionSchemeId,
                Comments = string.Empty,
                distributionStructure.TestConsultantTabulationId,
                distributionStructure.SchemeVersionDate,
                distributionStructure.DateOfReceipt,
                distributionStructure.StorageConditions,
                distributionStructure.ConditionOnReceipt,
                distributionStructure.Instructions,
                distributionStructure.StandardTabulationText,
            },
            transaction);

        await InsertTestsAsync(connection, transaction, monthlyDistributionSchemeId, distributionStructure);
        await InsertTabulationsAsync(connection, transaction, distributionStructure);
        await InsertSamplesAsync(connection, transaction, monthlyDistributionSchemeId, scheduleId, sampleNumberDate, distributionStructure);

        transaction.Commit();
        return true;
    }

    private static async Task InsertTestsAsync(
        System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, Guid monthlyDistributionSchemeId, DistributionStructure distributionStructure)
    {
        foreach (var test in distributionStructure.Tests)
        {
            await connection.ExecuteAsync(
                "EXEC dbo.spiDistributionTest @DistributionTestId = @TestId, @TestTypeId = @TestTypeId, @DistributionSchemeId = @DistributionSchemeId, @Order = @Order",
                new { test.TestId, test.TestTypeId, DistributionSchemeId = monthlyDistributionSchemeId, test.Order },
                transaction);

            // Legacy Test.Insert writes Method Items before Result Items.
            foreach (var item in test.MethodItems)
            {
                await connection.ExecuteAsync(
                    """
                    EXEC dbo.spiDistributionTestMethodItem
                        @DistributionTestMethodItemId = @TestMethodItemId,
                        @TestId = @TestId,
                        @TestMethodItemTypeId = @TestMethodItemTypeId,
                        @IntendedResult = @IntendedResult,
                        @Order = @Order,
                        @IsApplicable = @IsApplicable
                    """,
                    new { item.TestMethodItemId, item.TestId, item.TestMethodItemTypeId, item.IntendedResult, item.Order, item.IsApplicable },
                    transaction);
            }

            foreach (var item in test.ResultItems)
            {
                await connection.ExecuteAsync(
                    """
                    EXEC dbo.spiDistributionTestResultItem
                        @DistributionTestResultItemId = @TestResultItemId,
                        @TestId = @TestId,
                        @TestResultItemTypeId = @TestResultItemTypeId,
                        @Order = @Order,
                        @IsRange = @IsRange
                    """,
                    new { item.TestResultItemId, item.TestId, item.TestResultItemTypeId, item.Order, item.IsRange },
                    transaction);
            }

            foreach (var category in test.CategoryItems)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spiDistributionCategoryItem @CategoryItemId = @CategoryItemId, @TestId = @TestId, @CategoryItemTypeId = @CategoryItemTypeId, @Order = @Order",
                    new { category.CategoryItemId, category.TestId, category.CategoryItemTypeId, category.Order },
                    transaction);

                foreach (var criterion in category.CriterionItems)
                {
                    await connection.ExecuteAsync(
                        "EXEC dbo.spiDistributionCriterionItem @CriterionItemId = @CriterionItemId, @CategoryItemId = @CategoryItemId, @CriterionItemTypeId = @CriterionItemTypeId, @Order = @Order",
                        new { criterion.CriterionItemId, criterion.CategoryItemId, criterion.CriterionItemTypeId, criterion.Order },
                        transaction);
                }
            }
        }
    }

    private static async Task InsertTabulationsAsync(
        System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, DistributionStructure distributionStructure)
    {
        foreach (var tabulation in distributionStructure.Tabulations)
        {
            await connection.ExecuteAsync(
                """
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
                """,
                tabulation,
                transaction);

            foreach (var item in tabulation.MethodItems)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spiDistributionTabulationTestMethodItem @DistributionTabulationTestMethodItemId = @LinkId, @TabulationId = @TabulationId, @TestMethodItemId = @ItemId",
                    item,
                    transaction);
            }

            foreach (var item in tabulation.ResultItems)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spiDistributionTabulationTestResultItem @DistributionTabulationTestResultItemId = @LinkId, @TabulationId = @TabulationId, @TestResultItemId = @ItemId",
                    item,
                    transaction);
            }
        }
    }

    private static async Task InsertSamplesAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid monthlyDistributionSchemeId,
        Guid scheduleId,
        DateTime sampleNumberDate,
        DistributionStructure distributionStructure)
    {
        foreach (var sample in distributionStructure.Samples)
        {
            // Enlisted in the same transaction: spgNextSampleNumber ADVANCES a counter on
            // tblSchedule, so a rollback must give the numbers back.
            var next = await connection.ExecuteScalarAsync<int>(
                "EXEC dbo.spgNextSampleNumber @ScheduleId = @ScheduleId, @DistributionDate = @DistributionDate",
                new { ScheduleId = scheduleId, DistributionDate = sampleNumberDate },
                transaction);

            sample.SampleNumber = DistributionStructureBuilder.FormatSampleNumber(next, sampleNumberDate);

            await connection.ExecuteAsync(
                """
                EXEC dbo.spiSample
                    @SampleId = @SampleId,
                    @DistributionSchemeId = @DistributionSchemeId,
                    @SampleNumber = @SampleNumber,
                    @InternalDescription = '',
                    @ExternalDescription = '',
                    @TabulationDescription = '',
                    @TestConsultantsDescription = ''
                """,
                new { sample.SampleId, DistributionSchemeId = monthlyDistributionSchemeId, sample.SampleNumber },
                transaction);

            foreach (var sampleTest in sample.SampleTests)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spiSampleTest @SampleTestId = @SampleTestId, @SampleId = @SampleId, @TestId = @TestId, @IsApplicable = @IsApplicable",
                    new { sampleTest.SampleTestId, sampleTest.SampleId, sampleTest.TestId, sampleTest.IsApplicable },
                    transaction);

                foreach (var resultItem in sampleTest.ResultItems)
                {
                    await connection.ExecuteAsync(
                        """
                        EXEC dbo.spiSampleResultItem
                            @SampleResultItemId = @SampleResultItemId,
                            @SampleTestId = @SampleTestId,
                            @TestResultItemId = @TestResultItemId,
                            @IntendedResult = '',
                            @IntendedResult2 = ''
                        """,
                        new { resultItem.SampleResultItemId, resultItem.SampleTestId, resultItem.TestResultItemId },
                        transaction);
                }
            }
        }
    }

    public async Task UpdateIntendedResultsAsync(IntendedResultsWrite write, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            EXEC dbo.spuIntendedResults
                @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                @Comments = @Comments,
                @HasIntendedResults = @HasIntendedResults,
                @HasSampleNumbersDefined = @HasSampleNumbersDefined,
                @TestConsultantTabulationId = @TestConsultantTabulationId
            """,
            write.Header,
            transaction);

        var auditedTestMethodItemIds = write.AuditedTestMethodItemIds.ToHashSet();
        const string auditTestMethodItemSql = """
            EXEC dbo.spiAuditDistributionTestMethodItem
                @DistributionTestMethodItemId = @TestMethodItemId,
                @AuditWho = @AuditWho,
                @AuditWhy = @AuditWhy,
                @IntendedResult = @IntendedResult,
                @IntendedResultUpdate = 1
            """;

        const string updateTestMethodItemSql = """
            EXEC dbo.spuDistributionTestMethodItem
                @DistributionTestMethodItemId = @TestMethodItemId,
                @TestId = @TestId,
                @TestMethodItemTypeId = @TestMethodItemTypeId,
                @IntendedResult = @IntendedResult,
                @IsApplicable = @IsApplicable,
                @Order = @Order
            """;

        foreach (var methodItem in write.TestMethodItems)
        {
            // Must precede spuDistributionTestMethodItem: the audit procedure records the row's
            // CURRENT (pre-update) IntendedResult value.
            if (auditedTestMethodItemIds.Contains(methodItem.TestMethodItemId))
            {
                await connection.ExecuteAsync(
                    auditTestMethodItemSql,
                    new { methodItem.TestMethodItemId, write.AuditWho, write.AuditWhy, methodItem.IntendedResult },
                    transaction);
            }

            await connection.ExecuteAsync(updateTestMethodItemSql, methodItem, transaction);
        }

        const string updateTestResultItemSql = """
            EXEC dbo.spuDistributionTestResultItem
                @DistributionTestResultItemId = @TestResultItemId,
                @TestId = @TestId,
                @TestResultItemTypeId = @TestResultItemTypeId,
                @Order = @Order,
                @IsRange = @IsRange
            """;

        foreach (var resultItem in write.TestResultItems)
        {
            await connection.ExecuteAsync(updateTestResultItemSql, resultItem, transaction);
        }

        var auditedSampleResultItems = write.AuditedSampleResultItems.ToDictionary(a => a.SampleResultItemId);
        const string auditSampleResultItemSql = """
            EXEC dbo.spiAuditSampleResultItem
                @SampleResultItemId = @SampleResultItemId,
                @AuditWho = @AuditWho,
                @AuditWhy = @AuditWhy,
                @IntendedResult = @IntendedResult,
                @IntendedResultUpdate = @IntendedResultUpdate,
                @IntendedResult2 = @IntendedResult2,
                @IntendedResult2Update = @IntendedResult2Update
            """;

        const string updateSampleResultItemSql = """
            EXEC dbo.spuSampleResultItem
                @SampleResultItemId = @SampleResultItemId,
                @SampleTestId = @SampleTestId,
                @TestResultItemId = @TestResultItemId,
                @IntendedResult = @IntendedResult,
                @IntendedResult2 = @IntendedResult2
            """;

        foreach (var sampleResultItem in write.SampleResultItems)
        {
            // Must precede spuSampleResultItem: the audit procedure records the row's CURRENT
            // (pre-update) values.
            if (auditedSampleResultItems.TryGetValue(sampleResultItem.SampleResultItemId, out var audited))
            {
                await connection.ExecuteAsync(
                    auditSampleResultItemSql,
                    new
                    {
                        sampleResultItem.SampleResultItemId,
                        write.AuditWho,
                        write.AuditWhy,
                        sampleResultItem.IntendedResult,
                        IntendedResultUpdate = audited.IntendedResultChanged,
                        sampleResultItem.IntendedResult2,
                        IntendedResult2Update = audited.IntendedResult2Changed,
                    },
                    transaction);
            }

            await connection.ExecuteAsync(updateSampleResultItemSql, sampleResultItem, transaction);
        }

        transaction.Commit();
    }

    public async Task<DistributionParticipantsData?> GetDistributionParticipantsAsync(
        Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistributionScheme @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId });

        var scheme = await results.ReadFirstOrDefaultAsync<MonthlyDistributionSchemeEntity>();
        if (scheme is null)
        {
            return null;
        }

        var participants = (await results.ReadAsync<DistributionParticipantEntity>()).ToList();
        var additionalParticipants = (await results.ReadAsync<AdditionalParticipantEntity>()).ToList();

        scheme.ParticipantCount = participants.Count;
        scheme.TotalSetsOfSamplesRequired = participants.Sum(p => p.NumberOfSetsRequired);

        return new DistributionParticipantsData(scheme, participants, additionalParticipants);
    }

    public async Task UpdateParticipantDeadlineAsync(DistributionParticipantEntity participant, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            """
            EXEC dbo.spuDistributionParticipant
                @DistributionParticipantId = @DistributionParticipantId,
                @ParticipantId = @ParticipantId,
                @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                @DeadlineDate = @DeadlineDate,
                @DeadlineChangeReason = @DeadlineChangeReason,
                @AdditionalParticipant = @AdditionalParticipant
            """,
            participant);
    }

    public async Task RepeatDistributionAsync(
        MonthlyDistributionSchemeEntity newScheme, IReadOnlyList<DistributionParticipantEntity> newParticipants, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            EXEC dbo.spiMonthlyDistributionScheme
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
            """,
            newScheme,
            transaction);

        const string insertParticipantSql = """
            EXEC dbo.spiDistributionParticipant
                @DistributionParticipantId = @DistributionParticipantId,
                @ParticipantId = @ParticipantId,
                @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                @DeadlineDate = @DeadlineDate,
                @DeadlineChangeReason = @DeadlineChangeReason,
                @AdditionalParticipant = @AdditionalParticipant
            """;

        foreach (var participant in newParticipants)
        {
            await connection.ExecuteAsync(insertParticipantSql, participant, transaction);
        }

        transaction.Commit();
    }

    public async Task<PreparationSheetData?> GetPreparationSheetAsync(Guid monthlyDistributionId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgPreparationSheet @MonthlyDistributionId = @MonthlyDistributionId",
            new { MonthlyDistributionId = monthlyDistributionId });

        var header = await results.ReadFirstOrDefaultAsync<PreparationHeaderEntity>();
        if (header is null)
        {
            return null;
        }

        var schemes = (await results.ReadAsync<PreparationSchemeEntity>()).ToList();
        var samples = (await results.ReadAsync<PreparationSampleEntity>()).ToList();

        var schemesById = schemes.ToDictionary(s => s.MonthlyDistributionSchemeId);
        foreach (var sample in samples)
        {
            if (schemesById.TryGetValue(sample.MonthlyDistributionSchemeId, out var scheme))
            {
                scheme.Samples.Add(sample);
            }
        }

        return new PreparationSheetData(monthlyDistributionId, header.YearId, header.MonthId, schemes);
    }

    public async Task UpdatePreparationAsync(IReadOnlyList<PreparationSchemeEntity> schemes, CancellationToken cancellationToken = default)
    {
        if (schemes.Count == 0)
        {
            return;
        }

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        const string updateSql = """
            EXEC dbo.spuMonthlyDistributionSchemePrep
                @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                @DiaryEntry = @DiaryEntry,
                @PrepStartDate = @PrepStartDate,
                @PrepCompleteDate = @PrepCompleteDate
            """;

        foreach (var scheme in schemes)
        {
            await connection.ExecuteAsync(updateSql, scheme, transaction);
        }

        transaction.Commit();
    }

    public async Task<ResultsReceiptData?> GetResultsReceiptAsync(int yearId, int monthId, CancellationToken cancellationToken = default)
    {
        // @ReferenceDate is left to its NULL default - the stored procedure then uses today's date.
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistributionForResultsReceipt @YearId = @YearId, @MonthId = @MonthId",
            new { YearId = yearId, MonthId = monthId });

        var header = await results.ReadFirstOrDefaultAsync<ResultsReceiptHeaderEntity>();
        if (header is null)
        {
            return null;
        }

        var schemes = (await results.ReadAsync<ResultsReceiptSchemeEntity>()).ToList();
        var participants = (await results.ReadAsync<ResultsReceiptParticipantEntity>()).ToList();
        var statuses = (await results.ReadAsync<ResultsReceiptSampleStatusEntity>()).ToDictionary(s => s.DistributionParticipantId, s => s.HasUnaccountedSamples);

        var schemesById = schemes.ToDictionary(s => s.MonthlyDistributionSchemeId);
        foreach (var participant in participants)
        {
            if (statuses.TryGetValue(participant.DistributionParticipantId, out var hasUnaccounted))
            {
                participant.HasUnaccountedSamples = hasUnaccounted;
            }

            if (schemesById.TryGetValue(participant.MonthlyDistributionSchemeId, out var scheme))
            {
                scheme.Participants.Add(participant);
            }
        }

        return new ResultsReceiptData(header.MonthlyDistributionId, header.YearId, header.MonthId, schemes);
    }

    public async Task<IReadOnlyList<TabulationsSchemeEntity>> GetTabulationsAsync(Guid monthlyDistributionId, CancellationToken cancellationToken = default)
    {
        // @ReferenceDate is left to its NULL default - the stored procedure then uses today's date.
        using var connection = connectionFactory.CreateConnection();
        List<TabulationsSchemeEntity> schemes;

        using (var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistributionInfoForTabulationsById @MonthlyDistributionId = @MonthlyDistributionId",
            new { MonthlyDistributionId = monthlyDistributionId }))
        {
            // The first result set is the month header, which the caller already has.
            _ = await results.ReadAsync();
            schemes = (await results.ReadAsync<TabulationsSchemeEntity>()).ToList();
            var tabulations = (await results.ReadAsync<TabulationEntity>()).ToList();

            var schemesById = schemes.ToDictionary(s => s.MonthlyDistributionSchemeId);
            foreach (var tabulation in tabulations)
            {
                if (schemesById.TryGetValue(tabulation.MonthlyDistributionSchemeId, out var scheme))
                {
                    scheme.Tabulations.Add(tabulation);
                }
            }
        }

        // Legacy reads this for every scheme but only consults it when the scheme requires assessment.
        foreach (var scheme in schemes.Where(s => s.RequiresAssessment))
        {
            scheme.AssessmentStatuses = (await connection.QueryAsync<TabulationAssessmentStatusEntity>(
                "EXEC dbo.spgAssessmentStatus @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
                new { scheme.MonthlyDistributionSchemeId })).ToList();
        }

        return schemes;
    }

    public async Task UpdateResultsReceiptAsync(IReadOnlyList<ResultsReceiptParticipantEntity> participants, CancellationToken cancellationToken = default)
    {
        if (participants.Count == 0)
        {
            return;
        }

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        const string updateSql = """
            EXEC dbo.spuDistributionParticipantForResultsReceipt
                @DistributionParticipantId = @DistributionParticipantId,
                @ExtraDocumentsReturned = @ExtraDocumentsReturned,
                @ReturnedDate = @ReturnedDate
            """;

        foreach (var participant in participants)
        {
            await connection.ExecuteAsync(updateSql, participant, transaction);
        }

        transaction.Commit();
    }

    public async Task<SampleReceiptData?> GetSampleReceiptAsync(
        Guid distributionParticipantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgSampleReceipt @DistributionParticipantId = @DistributionParticipantId, @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
            new { DistributionParticipantId = distributionParticipantId, MonthlyDistributionSchemeId = monthlyDistributionSchemeId });

        var tests = (await results.ReadAsync<SampleReceiptTestEntity>()).ToList();
        var samples = (await results.ReadAsync<SampleReceiptSampleEntity>()).ToList();
        var lab = await results.ReadFirstOrDefaultAsync<SampleReceiptLabEntity>();
        var scheme = await results.ReadFirstOrDefaultAsync<SampleReceiptSchemeEntity>();

        return lab is null || scheme is null ? null : new SampleReceiptData(scheme, lab, tests, samples);
    }

    public async Task UpdateSampleReceiptAsync(IReadOnlyList<SampleReceiptSampleEntity> samples, CancellationToken cancellationToken = default)
    {
        if (samples.Count == 0)
        {
            return;
        }

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        const string updateSql = """
            EXEC dbo.spuSampleReceipt
                @SampleReceiptId = @SampleReceiptId,
                @SubmissionDate = @SubmissionDate,
                @NonSubmissionDate = @NonSubmissionDate,
                @NonCapableDate = @NonCapableDate
            """;

        foreach (var sample in samples)
        {
            await connection.ExecuteAsync(updateSql, sample, transaction);
        }

        transaction.Commit();
    }

    public async Task<PackagingRawData?> GetPackagingSheetAsync(
        Guid monthlyDistributionId, PackagingMode mode, int week, CancellationToken cancellationToken = default)
    {
        // @ReferenceDate is left to its NULL default - the stored procedures then use today's date
        // (legacy only overrides it from the ResultsTestDate test-harness appSetting).
        var (sql, parameters) = mode switch
        {
            PackagingMode.GroupAddress => (
                "EXEC dbo.spgGroupedPackagingSheet @MonthlyDistributionId = @MonthlyDistributionId, @WeekNumber = @WeekNumber",
                new { MonthlyDistributionId = monthlyDistributionId, WeekNumber = week }),
            PackagingMode.ParticipantCombined => (
                "EXEC dbo.spgPartGroupedPackagingSheet @MonthlyDistributionId = @MonthlyDistributionId, @WeekNumber = @WeekNumber",
                new { MonthlyDistributionId = monthlyDistributionId, WeekNumber = week }),
            _ => (
                "EXEC dbo.spgWeeklyPackagingSheet @MonthlyDistributionId = @MonthlyDistributionId",
                new { MonthlyDistributionId = monthlyDistributionId, WeekNumber = 0 }),
        };

        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(sql, parameters);

        var header = await results.ReadFirstOrDefaultAsync<PreparationHeaderEntity>();
        if (header is null)
        {
            return null;
        }

        var groups = (await results.ReadAsync<PackagingGroupEntity>()).ToList();

        if (mode == PackagingMode.ParticipantCombined)
        {
            var schemeItems = (await results.ReadAsync<PackagingSchemeItemEntity>()).ToList();
            return new PackagingRawData(header.YearId, header.MonthId, groups, [], [], schemeItems);
        }

        var items = (await results.ReadAsync<PackagingItemEntity>()).ToList();
        var participants = (await results.ReadAsync<PackagingParticipantEntity>()).ToList();
        return new PackagingRawData(header.YearId, header.MonthId, groups, items, participants, []);
    }

    public async Task UpdatePackagingAsync(
        IReadOnlyList<PackagingProgressUpdate> updates,
        IReadOnlyList<DistributionPostedEmailEntity> emails,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        const string updateSql = """
            EXEC dbo.spuDistributionParticipantPacking
                @DistributionParticipantId = @DistributionParticipantId,
                @PackagedDate = @PackagedDate,
                @PostedDate = @PostedDate
            """;

        foreach (var update in updates)
        {
            await connection.ExecuteAsync(updateSql, update, transaction);
        }

        const string insertEmailSql = """
            EXEC dbo.spiDistributionPostedEmail
                @DistributionPostedEmailId = @DistributionPostedEmailId,
                @ParticipantId = @ParticipantId,
                @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                @Email = @Email,
                @Distribution = @Distribution,
                @SchemeName = @SchemeName
            """;

        foreach (var email in emails)
        {
            await connection.ExecuteAsync(insertEmailSql, email, transaction);
        }

        transaction.Commit();
    }

    public async Task<ActualResultsData?> GetActualResultsAsync(Guid distributionParticipantId, CancellationToken cancellationToken = default)
    {
        // @ReferenceDate is left to its NULL default - the procedure then uses today's date
        // (legacy only overrides it from the ResultsTestDate test-harness appSetting).
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgActualResults @ActualResultsId = @ActualResultsId",
            new { ActualResultsId = distributionParticipantId });

        var header = await results.ReadFirstOrDefaultAsync<ActualResultsHeaderEntity>();
        if (header is null)
        {
            return null;
        }

        var sampleResults = (await results.ReadAsync<ActualSampleResultEntity>()).ToList();
        var methodItemResults = (await results.ReadAsync<ActualMethodItemResultEntity>()).ToList();
        return new ActualResultsData(header, sampleResults, methodItemResults);
    }

    public async Task<IReadOnlyList<Guid>> GetRatingIdsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<Guid>("EXEC dbo.spgaRating")).ToList();
    }

    public async Task SaveActualResultsAsync(ActualResultsWrite write, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var actualResultsParameters = new
        {
            ActualResultsId = write.DistributionParticipantId,
            write.FirstEditDate,
            write.LastEditDate,
        };

        await connection.ExecuteAsync(
            write.IsNew
                ? "EXEC dbo.spiActualResults @ActualResultsId = @ActualResultsId, @FirstEditDate = @FirstEditDate, @LastEditDate = @LastEditDate"
                : "EXEC dbo.spuActualResults @ActualResultsId = @ActualResultsId, @FirstEditDate = @FirstEditDate, @LastEditDate = @LastEditDate",
            actualResultsParameters,
            transaction);

        // The audit procedures record the row's CURRENT (pre-update) value, so they must run
        // immediately BEFORE the matching spu* call.
        foreach (var item in write.MethodItems)
        {
            var parameters = new { ActualResultsId = write.DistributionParticipantId, TestMethodItemId = item.ItemId, item.Result, write.AuditWho, write.AuditWhy };

            if (item.IsInsert)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spiActualMethodItemResult @ActualResultsId = @ActualResultsId, @TestMethodItemId = @TestMethodItemId, @Result = @Result",
                    parameters,
                    transaction);
                continue;
            }

            await connection.ExecuteAsync(
                """
                EXEC dbo.spiAuditActualTestMethodItem
                    @ActualResultsId = @ActualResultsId,
                    @TestMethodItemId = @TestMethodItemId,
                    @AuditWho = @AuditWho,
                    @AuditWhy = @AuditWhy,
                    @Result = @Result,
                    @ResultUpdate = 1
                """,
                parameters,
                transaction);

            await connection.ExecuteAsync(
                "EXEC dbo.spuActualMethodItemResult @ActualResultsId = @ActualResultsId, @TestMethodItemId = @TestMethodItemId, @Result = @Result",
                parameters,
                transaction);
        }

        foreach (var item in write.SampleItems)
        {
            var parameters = new { ActualResultsId = write.DistributionParticipantId, SampleResultItemId = item.ItemId, item.Result, write.AuditWho, write.AuditWhy };

            if (item.IsInsert)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spiActualResultItemResult @ActualResultsId = @ActualResultsId, @SampleResultItemId = @SampleResultItemId, @Result = @Result",
                    parameters,
                    transaction);
                continue;
            }

            await connection.ExecuteAsync(
                """
                EXEC dbo.spiAuditActualSampleResultItem
                    @ActualResultsId = @ActualResultsId,
                    @SampleResultItemId = @SampleResultItemId,
                    @AuditWho = @AuditWho,
                    @AuditWhy = @AuditWhy,
                    @Result = @Result,
                    @ResultUpdate = 1
                """,
                parameters,
                transaction);

            await connection.ExecuteAsync(
                "EXEC dbo.spuActualResultItemResult @ActualResultsId = @ActualResultsId, @SampleResultItemId = @SampleResultItemId, @Result = @Result",
                parameters,
                transaction);
        }

        if (write.DefaultRatingId is { } ratingId)
        {
            foreach (var sampleId in write.RatingSampleIds)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spiSampleRating @SampleId = @SampleId, @ParticipantId = @ParticipantId, @RatingId = @RatingId",
                    new { SampleId = sampleId, ParticipantId = write.DistributionParticipantId, RatingId = ratingId },
                    transaction);
            }
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<DistributionPostedEmailEntity>> GetUnsentDistributionPostedEmailsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<DistributionPostedEmailEntity>("EXEC dbo.spgaDistributionPostedEmail")).ToList();
    }

    public async Task MarkDistributionPostedEmailSentAsync(Guid distributionPostedEmailId, DateTime dateSent, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuDistributionPostedEmail @DistributionPostedEmailId = @DistributionPostedEmailId, @DateSent = @DateSent",
            new { DistributionPostedEmailId = distributionPostedEmailId, DateSent = dateSent });
    }

    public async Task<PackagingLabelAddressEntity?> GetLabelParticipantAddressAsync(
        Guid participantId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<PackagingLabelAddressEntity>(
            "EXEC dbo.spgLabelParticipantAddress @ParticipantId = @ParticipantId, @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
            new { ParticipantId = participantId, MonthlyDistributionSchemeId = monthlyDistributionSchemeId });
    }

    public async Task<PackagingLabelAddressEntity?> GetLabelGroupAddressAsync(Guid groupAddressId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<PackagingLabelAddressEntity>(
            "EXEC dbo.spgLabelGroupAddress @GroupAddressId = @GroupAddressId",
            new { GroupAddressId = groupAddressId });
    }
}

// spgMonthlyDistribution result set 1 - only present when the month has been initialised.
internal sealed class MonthlyDistributionHeaderEntity
{
    public Guid MonthlyDistributionId { get; set; }
    public int YearId { get; set; }
    public int MonthId { get; set; }
}
