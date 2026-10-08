using System.Data;
using Dapper;
using PTL.Core.Scheme;
using PTL.Data.Infrastructure;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Data.Scheme;

public sealed class SchemeRepository(IDbConnectionFactory connectionFactory) : ISchemeRepository
{
    public async Task<CoreScheme?> GetByIdAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // spgSchemeBySchemeId returns eleven result sets, in this order: Scheme, SchemeCurrency,
        // Test, TestMethodItem, TestResultItem, CategoryItem, CriterionItem, Tabulation,
        // TabulationTestMethodItem, TabulationTestResultItem, Viewers. Note method items come
        // before result items.
        using var reader = await connection.QueryMultipleAsync(
            "EXEC dbo.spgSchemeBySchemeId @SchemeId",
            new { SchemeId = schemeId });

        var scheme = (await reader.ReadAsync<CoreScheme>()).SingleOrDefault();
        if (scheme is null)
        {
            return null;
        }

        scheme.Prices = reader.IsConsumed ? [] : (await reader.ReadAsync<SchemeCurrencyPrice>()).ToList();
        if (reader.IsConsumed)
        {
            return scheme;
        }

        scheme.Tests = await ReadTestsAsync(reader);
        if (reader.IsConsumed)
        {
            return scheme;
        }

        scheme.Tabulations = await ReadTabulationsAsync(reader);
        return scheme;
    }

    // Test, TestMethodItem, TestResultItem, CategoryItem, CriterionItem result sets, assembled
    // into the Tests tree - split out of GetByIdAsync to keep that method's branching readable.
    private static async Task<List<SchemeTest>> ReadTestsAsync(SqlMapper.GridReader reader)
    {
        var tests = (await reader.ReadAsync<SchemeTest>()).ToList();
        var methodItems = (await reader.ReadAsync<SchemeTestMethodItem>()).ToList();
        var resultItems = (await reader.ReadAsync<SchemeTestResultItem>()).ToList();
        var categories = (await reader.ReadAsync<SchemeCategoryItem>()).ToList();
        var criteria = (await reader.ReadAsync<SchemeCriterionItem>()).ToList();

        foreach (var category in categories)
        {
            category.Criteria = criteria.Where(c => c.CategoryItemId == category.CategoryItemId).OrderBy(c => c.Order).ToList();
        }

        foreach (var test in tests)
        {
            test.MethodItems = methodItems.Where(m => m.TestId == test.TestId).OrderBy(m => m.Order).ToList();
            test.ResultItems = resultItems.Where(r => r.TestId == test.TestId).OrderBy(r => r.Order).ToList();
            test.Categories = categories.Where(c => c.TestId == test.TestId).OrderBy(c => c.Order).ToList();
        }

        return [.. tests.OrderBy(t => t.Order)];
    }

    // Tabulation, TabulationTestMethodItem, TabulationTestResultItem result sets, assembled into
    // the Results Tabulations tree - split out of GetByIdAsync for the same reason as ReadTestsAsync.
    private static async Task<List<SchemeTabulation>> ReadTabulationsAsync(SqlMapper.GridReader reader)
    {
        var tabulations = (await reader.ReadAsync<SchemeTabulation>()).ToList();
        var methodLinks = (await reader.ReadAsync<SchemeTabulationItemLink>()).ToList();
        var resultLinks = (await reader.ReadAsync<SchemeTabulationItemLink>()).ToList();

        foreach (var tabulation in tabulations)
        {
            tabulation.MethodItemLinks = methodLinks.Where(l => l.TabulationId == tabulation.TabulationId).ToList();
            tabulation.ResultItemLinks = resultLinks.Where(l => l.TabulationId == tabulation.TabulationId).ToList();
            tabulation.MethodItemIds = tabulation.MethodItemLinks.Select(l => l.ItemId).ToList();
            tabulation.ResultItemIds = tabulation.ResultItemLinks.Select(l => l.ItemId).ToList();
        }

        return tabulations;
    }

    public async Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<SchemeSummaryEntity>(
            "EXEC dbo.spgSchemeInfoByYearId @YearId",
            new { YearId = yearId })).ToList();
    }

    public async Task<IReadOnlyList<SchemeSummaryEntity>> GetAllSummariesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<SchemeSummaryEntity>("EXEC dbo.spgaSchemeInfo")).ToList();
    }

    public async Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesBySchemeIdAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<SchemeSummaryEntity>(
            "EXEC dbo.spgSchemeInfoBySchemeId @SchemeId",
            new { SchemeId = schemeId })).ToList();
    }

    public async Task<IReadOnlyList<SchemeHistoryEntity>> GetHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<SchemeHistoryEntity>(
            "EXEC dbo.spgSchemeInfoBySharedId @SharedId",
            new { SharedId = sharedId })).ToList();
    }

    public async Task<CoreScheme> CreateAsync(CoreScheme scheme, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(InsertSql, BuildParameters(scheme));
        await SavePricesAsync(connection, scheme);
        await SaveChildCollectionsAsync(connection, scheme, cancellationToken);
        var created = await GetByIdAsync(scheme.SchemeId, cancellationToken);
        return created ?? throw new InvalidOperationException($"Scheme {scheme.SchemeId} was inserted but could not be re-read.");
    }

    public async Task<CoreScheme?> UpdateAsync(CoreScheme scheme, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(UpdateSql, BuildParameters(scheme));
        if (rowsAffected == 0)
        {
            return null;
        }

        await SavePricesAsync(connection, scheme);
        await SaveChildCollectionsAsync(connection, scheme, cancellationToken);
        return await GetByIdAsync(scheme.SchemeId, cancellationToken);
    }

    // Legacy SchemeCurrencyCollection.Update: insert rows that have no id yet, update the rest.
    // There is no spdSchemeCurrency, so a currency link is never deleted. Arguments are bound by
    // name - a positional EXEC would silently depend on the procedure's declaration order.
    private static async Task SavePricesAsync(System.Data.IDbConnection connection, CoreScheme scheme)
    {
        foreach (var price in scheme.Prices)
        {
            var isNew = price.SchemeCurrencyId == Guid.Empty;
            if (isNew)
            {
                price.SchemeCurrencyId = Guid.NewGuid();
            }

            var sql = isNew
                ? "EXEC dbo.spiSchemeCurrency @SchemeCurrencyId = @SchemeCurrencyId, @SchemeId = @SchemeId, @CurrencyId = @CurrencyId, @Price = @Price"
                : "EXEC dbo.spuSchemeCurrency @SchemeCurrencyId = @SchemeCurrencyId, @SchemeId = @SchemeId, @CurrencyId = @CurrencyId, @Price = @Price";

            await connection.ExecuteAsync(sql, new
            {
                price.SchemeCurrencyId,
                scheme.SchemeId,
                price.CurrencyId,
                price.Price,
            });
        }
    }

    // The Tests and Results Tabulations tabs are staged in the form and written as a unit, in
    // legacy's order: Tests.UpdateNotDelete, then Tabulations.Update, then Tests.UpdateDeleteOnly.
    // Deleting tests last matters because tabulation link rows reference their items.
    private async Task SaveChildCollectionsAsync(System.Data.IDbConnection connection, CoreScheme scheme, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(scheme.SchemeId, cancellationToken);
        var existingTests = existing?.Tests ?? [];

        await SaveTestsAsync(connection, scheme, existingTests);
        await SaveTabulationsAsync(connection, scheme, existing?.Tabulations ?? []);
        await DeleteRemovedAsync(connection, scheme, existingTests);
    }

    // Ids are generated client-side when an item is staged (legacy does the same), so a row is
    // new when it is not already present rather than when its id is empty.
    private static async Task SaveTestsAsync(System.Data.IDbConnection connection, CoreScheme scheme, IList<SchemeTest> existingTests)
    {
        var existingTestIds = existingTests.Select(t => t.TestId).ToHashSet();
        var existingResultItemIds = existingTests.SelectMany(t => t.ResultItems).Select(i => i.TestResultItemId).ToHashSet();
        var existingMethodItemIds = existingTests.SelectMany(t => t.MethodItems).Select(i => i.TestMethodItemId).ToHashSet();
        var existingCategoryIds = existingTests.SelectMany(t => t.Categories).Select(c => c.CategoryItemId).ToHashSet();
        var existingCriterionIds = existingTests.SelectMany(t => t.Categories).SelectMany(c => c.Criteria).Select(c => c.CriterionItemId).ToHashSet();

        for (var testIndex = 0; testIndex < scheme.Tests.Count; testIndex++)
        {
            var test = scheme.Tests[testIndex];
            test.SchemeId = scheme.SchemeId;
            test.Order = testIndex + 1;
            if (test.TestId == Guid.Empty)
            {
                test.TestId = Guid.NewGuid();
            }

            await UpsertAsync(connection, !existingTestIds.Contains(test.TestId),
                "EXEC dbo.spiTest @TestId = @TestId, @TestTypeId = @TestTypeId, @SchemeId = @SchemeId, @Order = @Order",
                "EXEC dbo.spuTest @TestId = @TestId, @TestTypeId = @TestTypeId, @SchemeId = @SchemeId, @Order = @Order",
                () => new { test.TestId, test.TestTypeId, test.SchemeId, test.Order });

            await SaveTestChildrenAsync(connection, test, existingResultItemIds, existingMethodItemIds, existingCategoryIds, existingCriterionIds);
        }
    }

    // Legacy TabulationCollection.Update: upsert each tabulation, reconcile its item links by
    // insert/delete (neither link table has an update procedure), then delete tabulations that
    // are no longer present.
    private static async Task SaveTabulationsAsync(System.Data.IDbConnection connection, CoreScheme scheme, IList<SchemeTabulation> existingTabulations)
    {
        foreach (var tabulation in scheme.Tabulations)
        {
            tabulation.SchemeId = scheme.SchemeId;
            if (tabulation.TabulationId == Guid.Empty)
            {
                tabulation.TabulationId = Guid.NewGuid();
            }

            var isNew = existingTabulations.All(t => t.TabulationId != tabulation.TabulationId);
            await UpsertAsync(connection, isNew,
                "EXEC dbo.spiTabulation @TabulationId = @TabulationId, @SchemeId = @SchemeId, @Name = @Name, @IntendedResultsOnly = @IntendedResultsOnly, @SingleParticipantTabulation = @SingleParticipantTabulation, @ShowRatings = @ShowRatings, @AvailableToParticipants = @AvailableToParticipants, @AvailableToViewers = @AvailableToViewers",
                "EXEC dbo.spuTabulation @TabulationId = @TabulationId, @SchemeId = @SchemeId, @Name = @Name, @IntendedResultsOnly = @IntendedResultsOnly, @SingleParticipantTabulation = @SingleParticipantTabulation, @ShowRatings = @ShowRatings, @AvailableToParticipants = @AvailableToParticipants, @AvailableToViewers = @AvailableToViewers",
                () => new
                {
                    tabulation.TabulationId,
                    tabulation.SchemeId,
                    tabulation.Name,
                    tabulation.IntendedResultsOnly,
                    tabulation.SingleParticipantTabulation,
                    tabulation.ShowRatings,
                    tabulation.AvailableToParticipants,
                    tabulation.AvailableToViewers,
                });

            var existingLinks = existingTabulations.FirstOrDefault(t => t.TabulationId == tabulation.TabulationId);
            await SyncItemLinksAsync(connection, tabulation.TabulationId, tabulation.ResultItemIds, existingLinks?.ResultItemLinks ?? [],
                "EXEC dbo.spdTabulationTestResultItem @TabulationTestResultItemId = @LinkId",
                "EXEC dbo.spiTabulationTestResultItem @TabulationTestResultItemId = @LinkId, @TabulationId = @TabulationId, @TestResultItemId = @ItemId");
            await SyncItemLinksAsync(connection, tabulation.TabulationId, tabulation.MethodItemIds, existingLinks?.MethodItemLinks ?? [],
                "EXEC dbo.spdTabulationTestMethodItem @TabulationTestMethodItemId = @LinkId",
                "EXEC dbo.spiTabulationTestMethodItem @TabulationTestMethodItemId = @LinkId, @TabulationId = @TabulationId, @TestMethodItemId = @ItemId");
        }

        var keptTabulations = scheme.Tabulations.Select(t => t.TabulationId).ToHashSet();
        foreach (var removed in existingTabulations.Where(t => !keptTabulations.Contains(t.TabulationId)))
        {
            // spdTabulation cascades to both link tables.
            await connection.ExecuteAsync("EXEC dbo.spdTabulation @TabulationId", new { removed.TabulationId });
        }
    }

    // Deletes/inserts a tabulation's item links (TabulationTestResultItem/TabulationTestMethodItem).
    // The delete and insert SQL are complete literal command texts supplied by the caller - this
    // method only chooses between them and binds parameters, so there is no dynamically built SQL.
    private static async Task SyncItemLinksAsync(
        System.Data.IDbConnection connection,
        Guid tabulationId,
        IList<Guid> requestedItemIds,
        IList<SchemeTabulationItemLink> existingLinks,
        string deleteSql,
        string insertSql)
    {
        var requested = requestedItemIds.ToHashSet();

        foreach (var link in existingLinks.Where(l => !requested.Contains(l.ItemId)))
        {
            await connection.ExecuteAsync(deleteSql, new { LinkId = link.LinkId });
        }

        var alreadyLinked = existingLinks.Select(l => l.ItemId).ToHashSet();
        foreach (var itemId in requested.Where(id => !alreadyLinked.Contains(id)))
        {
            await connection.ExecuteAsync(insertSql, new { LinkId = Guid.NewGuid(), TabulationId = tabulationId, ItemId = itemId });
        }
    }

    private static async Task SaveTestChildrenAsync(
        System.Data.IDbConnection connection,
        SchemeTest test,
        HashSet<Guid> existingResultItemIds,
        HashSet<Guid> existingMethodItemIds,
        HashSet<Guid> existingCategoryIds,
        HashSet<Guid> existingCriterionIds)
    {
        for (var i = 0; i < test.ResultItems.Count; i++)
        {
            var item = test.ResultItems[i];
            item.TestId = test.TestId;
            item.Order = i + 1;
            if (item.TestResultItemId == Guid.Empty)
            {
                item.TestResultItemId = Guid.NewGuid();
            }

            await UpsertAsync(connection, !existingResultItemIds.Contains(item.TestResultItemId),
                "EXEC dbo.spiTestResultItem @TestResultItemId = @TestResultItemId, @TestResultItemTypeId = @TestResultItemTypeId, @TestId = @TestId, @Order = @Order",
                "EXEC dbo.spuTestResultItem @TestResultItemId = @TestResultItemId, @TestResultItemTypeId = @TestResultItemTypeId, @TestId = @TestId, @Order = @Order",
                () => new { item.TestResultItemId, item.TestResultItemTypeId, item.TestId, item.Order });
        }

        for (var i = 0; i < test.MethodItems.Count; i++)
        {
            var item = test.MethodItems[i];
            item.TestId = test.TestId;
            item.Order = i + 1;
            if (item.TestMethodItemId == Guid.Empty)
            {
                item.TestMethodItemId = Guid.NewGuid();
            }

            await UpsertAsync(connection, !existingMethodItemIds.Contains(item.TestMethodItemId),
                "EXEC dbo.spiTestMethodItem @TestMethodItemId = @TestMethodItemId, @TestMethodItemTypeId = @TestMethodItemTypeId, @TestId = @TestId, @Order = @Order",
                "EXEC dbo.spuTestMethodItem @TestMethodItemId = @TestMethodItemId, @TestMethodItemTypeId = @TestMethodItemTypeId, @TestId = @TestId, @Order = @Order",
                () => new { item.TestMethodItemId, item.TestMethodItemTypeId, item.TestId, item.Order });
        }

        for (var i = 0; i < test.Categories.Count; i++)
        {
            var category = test.Categories[i];
            category.TestId = test.TestId;
            category.Order = i + 1;
            if (category.CategoryItemId == Guid.Empty)
            {
                category.CategoryItemId = Guid.NewGuid();
            }

            // spuCategoryItem / spuCriterionItem only take the id and the order - the type and
            // parent are fixed at insert time.
            if (!existingCategoryIds.Contains(category.CategoryItemId))
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spiCategoryItem @CategoryItemId = @CategoryItemId, @CategoryItemTypeId = @CategoryItemTypeId, @TestId = @TestId, @Order = @Order",
                    new { category.CategoryItemId, category.CategoryItemTypeId, category.TestId, category.Order });
            }
            else
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spuCategoryItem @CategoryItemId = @CategoryItemId, @Order = @Order",
                    new { category.CategoryItemId, category.Order });
            }

            for (var j = 0; j < category.Criteria.Count; j++)
            {
                var criterion = category.Criteria[j];
                criterion.CategoryItemId = category.CategoryItemId;
                criterion.Order = j + 1;
                if (criterion.CriterionItemId == Guid.Empty)
                {
                    criterion.CriterionItemId = Guid.NewGuid();
                }

                if (!existingCriterionIds.Contains(criterion.CriterionItemId))
                {
                    await connection.ExecuteAsync(
                        "EXEC dbo.spiCriterionItem @CriterionItemId = @CriterionItemId, @CriterionItemTypeId = @CriterionItemTypeId, @CategoryItemId = @CategoryItemId, @Order = @Order",
                        new { criterion.CriterionItemId, criterion.CriterionItemTypeId, criterion.CategoryItemId, criterion.Order });
                }
                else
                {
                    await connection.ExecuteAsync(
                        "EXEC dbo.spuCriterionItem @CriterionItemId = @CriterionItemId, @Order = @Order",
                        new { criterion.CriterionItemId, criterion.Order });
                }
            }
        }
    }

    // The insert and update SQL are complete literal command texts supplied by the caller - this
    // method only chooses between them, so there is no dynamically built SQL to flag.
    private static async Task UpsertAsync(System.Data.IDbConnection connection, bool isNew, string insertSql, string updateSql, Func<object> parameters) =>
        await connection.ExecuteAsync(isNew ? insertSql : updateSql, parameters());

    private static async Task DeleteRemovedAsync(System.Data.IDbConnection connection, CoreScheme scheme, IList<SchemeTest> existingTests)
    {
        var keptTests = scheme.Tests.Select(t => t.TestId).ToHashSet();
        var keptResultItems = scheme.Tests.SelectMany(t => t.ResultItems).Select(i => i.TestResultItemId).ToHashSet();
        var keptMethodItems = scheme.Tests.SelectMany(t => t.MethodItems).Select(i => i.TestMethodItemId).ToHashSet();
        var keptCategories = scheme.Tests.SelectMany(t => t.Categories).Select(c => c.CategoryItemId).ToHashSet();
        var keptCriteria = scheme.Tests.SelectMany(t => t.Categories).SelectMany(c => c.Criteria).Select(c => c.CriterionItemId).ToHashSet();

        foreach (var test in existingTests)
        {
            foreach (var criterion in test.Categories.SelectMany(c => c.Criteria).Where(c => !keptCriteria.Contains(c.CriterionItemId)))
            {
                await connection.ExecuteAsync("EXEC dbo.spdCriterionItem @CriterionItemId", new { criterion.CriterionItemId });
            }

            foreach (var category in test.Categories.Where(c => !keptCategories.Contains(c.CategoryItemId)))
            {
                await connection.ExecuteAsync("EXEC dbo.spdCategoryItem @CategoryItemId", new { category.CategoryItemId });
            }

            foreach (var item in test.ResultItems.Where(i => !keptResultItems.Contains(i.TestResultItemId)))
            {
                await connection.ExecuteAsync("EXEC dbo.spdTestResultItem @TestResultItemId", new { item.TestResultItemId });
            }

            foreach (var item in test.MethodItems.Where(i => !keptMethodItems.Contains(i.TestMethodItemId)))
            {
                await connection.ExecuteAsync("EXEC dbo.spdTestMethodItem @TestMethodItemId", new { item.TestMethodItemId });
            }

            if (!keptTests.Contains(test.TestId))
            {
                await connection.ExecuteAsync("EXEC dbo.spdTest @TestId", new { test.TestId });
            }
        }
    }

    private const string InsertSql =
        "EXEC dbo.spiScheme @SchemeId, @SharedId, @YearId, @Identifier, @Name, @ScheduleId, @ScheduleCodeId, @StartDate, @DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, @DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, @DistributionAsAvailable, @WeekNumber, @DayOfWeekId, @Deadline, @Pilot, @Accredited, @ComerciallyAvailable, @LimitedSampleAvailability, @NoVLALabs, @CombinedPackaging, @SampleOrigin, @Subcontractor, @NumberOfSamples, @SamplePackingInstructions, @TestConsultant1, @TestConsultant2, @TestConsultant3, @CommentsRequired, @DateOfReceipt, @StorageConditions, @ConditionOnReceipt, @Instructions, @TestConsultantTabulationId, @UseExternalReference, @LastModified, @StoreRatings, @Assessor1, @Assessor2, @Assessor3, @Assessor4, @RequiresAssessment, @StandardTabulationText, @Postage, @CustomsDocumentDescription, @CustomsDocumentVolume, @DataConsentDeclarationActive, @DataConsentDeclarationText";

    private const string UpdateSql =
        "EXEC dbo.spuScheme @SchemeId, @SharedId, @YearId, @Identifier, @Name, @ScheduleId, @ScheduleCodeId, @StartDate, @DistributionMonthJan, @DistributionMonthFeb, @DistributionMonthMar, @DistributionMonthApr, @DistributionMonthMay, @DistributionMonthJun, @DistributionMonthJul, @DistributionMonthAug, @DistributionMonthSep, @DistributionMonthOct, @DistributionMonthNov, @DistributionMonthDec, @DistributionAsAvailable, @WeekNumber, @DayOfWeekId, @Deadline, @Pilot, @Accredited, @ComerciallyAvailable, @LimitedSampleAvailability, @NoVLALabs, @CombinedPackaging, @SampleOrigin, @Subcontractor, @NumberOfSamples, @SamplePackingInstructions, @TestConsultant1, @TestConsultant2, @TestConsultant3, @CommentsRequired, @DateOfReceipt, @StorageConditions, @ConditionOnReceipt, @Instructions, @TestConsultantTabulationId, @UseExternalReference, @LastModified, @StoreRatings, @Assessor1, @Assessor2, @Assessor3, @Assessor4, @RequiresAssessment, @StandardTabulationText, @Postage, @CustomsDocumentDescription, @CustomsDocumentVolume, @DataConsentDeclarationActive, @DataConsentDeclarationText";

    private static DynamicParameters BuildParameters(CoreScheme scheme)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@SchemeId", scheme.SchemeId);
        parameters.Add("@SharedId", scheme.SharedId);
        parameters.Add("@YearId", scheme.YearId);
        parameters.Add("@Identifier", scheme.Identifier);
        parameters.Add("@Name", scheme.Name);
        parameters.Add("@ScheduleId", scheme.ScheduleId);
        parameters.Add("@ScheduleCodeId", scheme.ScheduleCodeId);
        parameters.Add("@StartDate", scheme.StartDate);
        parameters.Add("@DistributionMonthJan", scheme.DistributionMonthJan);
        parameters.Add("@DistributionMonthFeb", scheme.DistributionMonthFeb);
        parameters.Add("@DistributionMonthMar", scheme.DistributionMonthMar);
        parameters.Add("@DistributionMonthApr", scheme.DistributionMonthApr);
        parameters.Add("@DistributionMonthMay", scheme.DistributionMonthMay);
        parameters.Add("@DistributionMonthJun", scheme.DistributionMonthJun);
        parameters.Add("@DistributionMonthJul", scheme.DistributionMonthJul);
        parameters.Add("@DistributionMonthAug", scheme.DistributionMonthAug);
        parameters.Add("@DistributionMonthSep", scheme.DistributionMonthSep);
        parameters.Add("@DistributionMonthOct", scheme.DistributionMonthOct);
        parameters.Add("@DistributionMonthNov", scheme.DistributionMonthNov);
        parameters.Add("@DistributionMonthDec", scheme.DistributionMonthDec);
        parameters.Add("@DistributionAsAvailable", scheme.DistributionAsAvailable);
        parameters.Add("@WeekNumber", scheme.WeekNumber);
        parameters.Add("@DayOfWeekId", scheme.DayOfWeekId);
        parameters.Add("@Deadline", scheme.Deadline);
        parameters.Add("@Pilot", scheme.Pilot);
        parameters.Add("@Accredited", scheme.Accredited);
        parameters.Add("@ComerciallyAvailable", scheme.ComerciallyAvailable);
        parameters.Add("@LimitedSampleAvailability", scheme.LimitedSampleAvailability);
        parameters.Add("@NoVLALabs", scheme.NoVLALabs);
        parameters.Add("@CombinedPackaging", scheme.CombinedPackaging);
        parameters.Add("@SampleOrigin", scheme.SampleOrigin);
        parameters.Add("@Subcontractor", scheme.Subcontractor);
        parameters.Add("@NumberOfSamples", scheme.NumberOfSamples);
        parameters.Add("@SamplePackingInstructions", scheme.SamplePackingInstructions);
        parameters.Add("@TestConsultant1", scheme.TestConsultant1);
        parameters.Add("@TestConsultant2", scheme.TestConsultant2);
        parameters.Add("@TestConsultant3", scheme.TestConsultant3);
        parameters.Add("@CommentsRequired", scheme.CommentsRequired);
        parameters.Add("@DateOfReceipt", scheme.DateOfReceipt);
        parameters.Add("@StorageConditions", scheme.StorageConditions);
        parameters.Add("@ConditionOnReceipt", scheme.ConditionOnReceipt);
        parameters.Add("@Instructions", scheme.Instructions);
        parameters.Add("@TestConsultantTabulationId", scheme.TestConsultantTabulationId);
        parameters.Add("@UseExternalReference", scheme.UseExternalReference);
        parameters.Add("@LastModified", scheme.LastModified);
        parameters.Add("@StoreRatings", scheme.StoreRatings);
        parameters.Add("@Assessor1", scheme.Assessor1);
        parameters.Add("@Assessor2", scheme.Assessor2);
        parameters.Add("@Assessor3", scheme.Assessor3);
        parameters.Add("@Assessor4", scheme.Assessor4);
        parameters.Add("@RequiresAssessment", scheme.RequiresAssessment);
        parameters.Add("@StandardTabulationText", scheme.StandardTabulationText);
        parameters.Add("@Postage", scheme.Postage);
        parameters.Add("@CustomsDocumentDescription", scheme.CustomsDescription);
        parameters.Add("@CustomsDocumentVolume", scheme.CustomsVolume);
        parameters.Add("@DataConsentDeclarationActive", scheme.DataConsentDeclarationActive);
        parameters.Add("@DataConsentDeclarationText", scheme.DataConsentDeclarationText);
        return parameters;
    }
}
