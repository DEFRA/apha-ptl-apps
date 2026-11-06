using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.Participant;
using PTL.Core.Scheme;
using static PTL.Api.Tests.Scheme.SchemeAuditFixture;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Scheme;

public class SchemeServiceAuditTests
{
    private static async Task<(SchemeService Service, FakeSchemeRepository Repository)> StoredSchemeAsync(CoreScheme? stored = null)
    {
        var repository = new FakeSchemeRepository();
        await repository.CreateAsync(stored ?? Build());
        var service = new SchemeService(repository, new FakeLookupRepository(), new FakeViewerRepository(), new FakeParticipantRepository(), NullLogger<SchemeService>.Instance);
        return (service, repository);
    }

    private static Task<SchemeUpdateResult> UpdateAsync(SchemeService service, CoreScheme updated, string? why = null, string who = "tester") =>
        service.UpdateSchemeAsync(SchemeId, updated, who, why);

    private static CoreScheme AuditableEdit()
    {
        var updated = Build();
        updated.Tests.RemoveAt(1);
        return updated;
    }

    // A closed-year scheme is read-only, but legacy still lets its Tests tab be re-ordered and a Category removed.
    private static CoreScheme ClosedYearScheme()
    {
        var scheme = Build();
        scheme.YearId = DateTime.UtcNow.Year - 1;
        return scheme;
    }

    private static void Swap<T>(IList<T> items) => (items[0], items[1]) = (items[1], items[0]);

    // The request for a closed-year scheme with one of the Tests tab edits legacy still allows.
    private static CoreScheme ClosedYearEdit(string edit)
    {
        var updated = ClosedYearScheme();
        var test = updated.Tests[0];

        switch (edit)
        {
            case "results":
                Swap(test.ResultItems);
                break;
            case "methods":
                Swap(test.MethodItems);
                break;
            case "categories":
                Swap(test.Categories);
                break;
            case "criteria":
                Swap(test.Categories[0].Criteria);
                break;
            case "category removed":
                test.Categories.RemoveAt(1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(edit), edit, null);
        }

        return updated;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateSchemeAsync_AuditableChangeWithoutAReason_AsksForOneAndSavesNothing(string? reason)
    {
        var (service, repository) = await StoredSchemeAsync();

        var result = await UpdateAsync(service, AuditableEdit(), reason);

        Assert.Equal(SchemeUpdateStatus.AuditReasonRequired, result.Status);
        Assert.Equal("Scheme PT1234: Test Scheme", result.AuditObjectName);
        Assert.Null(result.Scheme);
        Assert.Empty(repository.AuditWrites);
        Assert.Equal(2, (await repository.GetByIdAsync(SchemeId))!.Tests.Count);
    }

    [Fact]
    public async Task UpdateSchemeAsync_AuditObjectName_UsesThePostedIdentifierAndName()
    {
        var (service, _) = await StoredSchemeAsync();
        var updated = AuditableEdit();
        updated.Name = "Renamed Scheme";

        var result = await UpdateAsync(service, updated);

        Assert.Equal("Scheme PT1234: Renamed Scheme", result.AuditObjectName);
    }

    [Fact]
    public async Task UpdateSchemeAsync_AuditableChangeWithAReason_PassesTheAuditToTheRepository()
    {
        var (service, repository) = await StoredSchemeAsync();

        var result = await UpdateAsync(service, AuditableEdit(), "Test 2 was withdrawn");

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Single(result.Scheme!.Tests);
        var audit = Assert.Single(repository.AuditWrites);
        Assert.NotNull(audit);
        Assert.Equal("tester", audit.AuditWho);
        Assert.Equal("Test 2 was withdrawn", audit.AuditWhy);
        Assert.Equal(new SchemeItemAudit(Test2, SchemeAuditOperation.Delete, TypeB, false, 2, false), Assert.Single(audit.Plan.Tests));
        Assert.Single((await repository.GetByIdAsync(SchemeId))!.Tests);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReasonOfExactlyTheMaximumLength_IsAccepted()
    {
        var (service, repository) = await StoredSchemeAsync();
        var reason = new string('x', 2000);

        var result = await UpdateAsync(service, AuditableEdit(), reason);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Equal(reason, repository.LastAudit!.AuditWhy);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReasonLongerThanTheMaximum_ThrowsAValidationErrorOnAuditWhy()
    {
        var (service, repository) = await StoredSchemeAsync();

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => UpdateAsync(service, AuditableEdit(), new string('x', 2001)));

        var error = Assert.Single(exception.Errors);
        Assert.Equal("AuditWhy", error.Field);
        Assert.Equal(SchemeService.AuditWhyTooLongMessage, error.Message);
        Assert.Empty(repository.AuditWrites);
    }

    [Fact]
    public async Task UpdateSchemeAsync_AuditWhoLongerThanTheColumn_IsTruncatedToFiftyCharacters()
    {
        var (service, repository) = await StoredSchemeAsync();
        var who = new string('a', 30) + new string('b', 50);

        await UpdateAsync(service, AuditableEdit(), "Reason", who);

        Assert.Equal(who[..50], repository.LastAudit!.AuditWho);
    }

    [Fact]
    public async Task UpdateSchemeAsync_AuditWhoOfExactlyFiftyCharacters_IsKeptWhole()
    {
        var (service, repository) = await StoredSchemeAsync();
        var who = new string('a', 50);

        await UpdateAsync(service, AuditableEdit(), "Reason", who);

        Assert.Equal(who, repository.LastAudit!.AuditWho);
    }

    [Fact]
    public async Task UpdateSchemeAsync_OnlyNonAuditedFieldsChanged_SavesWithoutAReasonAndWithoutAnAudit()
    {
        var (service, repository) = await StoredSchemeAsync();
        var updated = Build();
        updated.Name = "Renamed";
        updated.Instructions = "Different instructions.";
        updated.Prices[0].Price = 99m;
        updated.ViewerIds.Add(Guid.NewGuid());
        updated.Tabulations[0].ShowRatings = true;
        updated.Tabulations[0].AvailableToViewers = false;
        updated.Assessor3 = Guid.NewGuid();

        var result = await UpdateAsync(service, updated);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Equal("Renamed", result.Scheme!.Name);
        Assert.Null(Assert.Single(repository.AuditWrites));
    }

    [Fact]
    public async Task UpdateSchemeAsync_OnlyNonAuditedFieldsChangedButAReasonSupplied_StillWritesNoAudit()
    {
        var (service, repository) = await StoredSchemeAsync();
        var updated = Build();
        updated.Name = "Renamed";

        var result = await UpdateAsync(service, updated, "Not needed");

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Null(Assert.Single(repository.AuditWrites));
    }

    [Fact]
    public async Task UpdateSchemeAsync_NothingChanged_SavesWithoutAnAudit()
    {
        var (service, repository) = await StoredSchemeAsync();

        var result = await UpdateAsync(service, Build());

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Null(Assert.Single(repository.AuditWrites));
    }

    [Fact]
    public async Task UpdateSchemeAsync_UnknownScheme_IsNotFoundAndNothingIsWritten()
    {
        var (service, repository) = await StoredSchemeAsync();

        var result = await service.UpdateSchemeAsync(Guid.NewGuid(), AuditableEdit(), "tester", "Reason");

        Assert.Equal(SchemeUpdateStatus.NotFound, result.Status);
        Assert.Empty(repository.AuditWrites);
    }

    [Fact]
    public async Task UpdateSchemeAsync_RepositoryReturnsNullForAnAuditedSave_IsNotFound()
    {
        var (service, repository) = await StoredSchemeAsync();
        repository.ForceUpdateReturnsNull = true;

        var result = await UpdateAsync(service, AuditableEdit(), "Reason");

        Assert.Equal(SchemeUpdateStatus.NotFound, result.Status);
        Assert.Null(result.Scheme);
        Assert.NotNull(Assert.Single(repository.AuditWrites));
    }

    [Theory]
    [InlineData("results")]
    [InlineData("methods")]
    [InlineData("categories")]
    [InlineData("criteria")]
    [InlineData("category removed")]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_AuditedChangeWithoutAReason_AsksForOneAndSavesNothing(string edit)
    {
        var (service, repository) = await StoredSchemeAsync(ClosedYearScheme());
        var before = JsonSerializer.Serialize(await repository.GetByIdAsync(SchemeId));
        var updated = ClosedYearEdit(edit);
        // Name is locked on a closed year, so the prompt still names the stored scheme.
        updated.Name = "Renamed Scheme";

        var result = await UpdateAsync(service, updated);

        Assert.Equal(SchemeUpdateStatus.AuditReasonRequired, result.Status);
        Assert.Equal("Scheme PT1234: Test Scheme", result.AuditObjectName);
        Assert.Null(result.Scheme);
        Assert.Empty(repository.AuditWrites);
        Assert.Equal(before, JsonSerializer.Serialize(await repository.GetByIdAsync(SchemeId)));
    }

    [Theory]
    [InlineData("results")]
    [InlineData("methods")]
    [InlineData("categories")]
    [InlineData("criteria")]
    [InlineData("category removed")]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_AuditedChangeWithAReason_SavesItAndWritesTheAudit(string edit)
    {
        var (service, repository) = await StoredSchemeAsync(ClosedYearScheme());

        var result = await UpdateAsync(service, ClosedYearEdit(edit), "Closed year clean-up");

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        var audit = Assert.Single(repository.AuditWrites);
        Assert.NotNull(audit);
        Assert.Equal("tester", audit.AuditWho);
        Assert.Equal("Closed year clean-up", audit.AuditWhy);
        Assert.False(audit.Plan.IsEmpty);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_ReorderedResultItems_AreSavedInTheNewOrderAndAudited()
    {
        var (service, repository) = await StoredSchemeAsync(ClosedYearScheme());

        var result = await UpdateAsync(service, ClosedYearEdit("results"), "Results re-ordered");

        Assert.Equal(new[] { Result2, Result1 }, result.Scheme!.Tests[0].ResultItems.Select(i => i.TestResultItemId));
        Assert.Equal(new[] { Result2, Result1 }, (await repository.GetByIdAsync(SchemeId))!.Tests[0].ResultItems.Select(i => i.TestResultItemId));
        var plan = repository.LastAudit!.Plan;
        Assert.Equal(2, plan.TestResultItems.Count);
        Assert.Contains(plan.TestResultItems, a => a == new SchemeItemAudit(Result2, SchemeAuditOperation.Update, TypeA, false, 1, true));
        Assert.Contains(plan.TestResultItems, a => a == new SchemeItemAudit(Result1, SchemeAuditOperation.Update, TypeA, false, 2, true));
        Assert.Empty(plan.Tests);
        Assert.Empty(plan.TestMethodItems);
        Assert.Empty(plan.CategoryItems);
        Assert.Empty(plan.CriterionItems);
        Assert.Empty(plan.Tabulations);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_RemovedCategory_IsSavedWithoutItAndAuditedAsADelete()
    {
        var (service, repository) = await StoredSchemeAsync(ClosedYearScheme());

        var result = await UpdateAsync(service, ClosedYearEdit("category removed"), "Timeliness category withdrawn");

        Assert.Equal(new[] { Category1 }, result.Scheme!.Tests[0].Categories.Select(c => c.CategoryItemId));
        Assert.Equal(new[] { Category1 }, (await repository.GetByIdAsync(SchemeId))!.Tests[0].Categories.Select(c => c.CategoryItemId));
        var plan = repository.LastAudit!.Plan;
        Assert.Equal(new SchemeItemAudit(Category2, SchemeAuditOperation.Delete, TypeA, false, 2, false), Assert.Single(plan.CategoryItems));
        Assert.Empty(plan.Tests);
        Assert.Empty(plan.CriterionItems);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_TestsAddedOrRemoved_AreIgnoredSoNoReasonIsNeeded()
    {
        var (service, repository) = await StoredSchemeAsync(ClosedYearScheme());
        var updated = ClosedYearScheme();
        updated.Tests.RemoveAt(1);
        updated.Tests.Add(new SchemeTest { TestTypeId = TypeA, MethodItems = [new SchemeTestMethodItem { TestMethodItemTypeId = TypeA }] });

        var result = await UpdateAsync(service, updated);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Null(Assert.Single(repository.AuditWrites));
        Assert.Equal(new[] { Test1, Test2 }, result.Scheme!.Tests.Select(t => t.TestId));
        Assert.Equal(new[] { Test1, Test2 }, (await repository.GetByIdAsync(SchemeId))!.Tests.Select(t => t.TestId));
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_ResultAndMethodItemsAddedOrRemovedFromTheEnd_AreIgnoredSoNoReasonIsNeeded()
    {
        var (service, repository) = await StoredSchemeAsync(ClosedYearScheme());
        var updated = ClosedYearScheme();
        updated.Tests[0].ResultItems.RemoveAt(1);
        updated.Tests[0].MethodItems.RemoveAt(1);
        updated.Tests[1].ResultItems.Add(new SchemeTestResultItem { TestResultItemTypeId = TypeB });
        updated.Tests[1].MethodItems.Add(new SchemeTestMethodItem { TestMethodItemTypeId = TypeB });

        var result = await UpdateAsync(service, updated);

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        Assert.Null(Assert.Single(repository.AuditWrites));
        var saved = (await repository.GetByIdAsync(SchemeId))!;
        Assert.Equal(new[] { Result1, Result2 }, saved.Tests[0].ResultItems.Select(i => i.TestResultItemId));
        Assert.Equal(new[] { Method1, Method2 }, saved.Tests[0].MethodItems.Select(i => i.TestMethodItemId));
        Assert.Equal(new[] { Result3 }, saved.Tests[1].ResultItems.Select(i => i.TestResultItemId));
        Assert.Equal(new[] { Method3 }, saved.Tests[1].MethodItems.Select(i => i.TestMethodItemId));
    }

    // Leaving out any item but the last moves it to the end, which is a re-order and so audited.
    [Fact]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_ResultItemOmittedFromTheFront_IsKeptAtTheEndAndAudited()
    {
        var (service, repository) = await StoredSchemeAsync(ClosedYearScheme());
        CoreScheme Edit()
        {
            var updated = ClosedYearScheme();
            updated.Tests[0].ResultItems.RemoveAt(0);
            return updated;
        }

        var withoutReason = await UpdateAsync(service, Edit());

        Assert.Equal(SchemeUpdateStatus.AuditReasonRequired, withoutReason.Status);
        Assert.Empty(repository.AuditWrites);

        var withReason = await UpdateAsync(service, Edit(), "Reason");

        Assert.Equal(SchemeUpdateStatus.Updated, withReason.Status);
        Assert.Equal(new[] { Result2, Result1 }, (await repository.GetByIdAsync(SchemeId))!.Tests[0].ResultItems.Select(i => i.TestResultItemId));
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_TabulationAdded_StillNeedsAReasonAndIsAudited()
    {
        var (service, repository) = await StoredSchemeAsync(ClosedYearScheme());

        CoreScheme Edit()
        {
            var updated = ClosedYearScheme();
            updated.Tabulations.Add(new SchemeTabulation { Name = "Extra" });
            return updated;
        }

        var withoutReason = await UpdateAsync(service, Edit());

        Assert.Equal(SchemeUpdateStatus.AuditReasonRequired, withoutReason.Status);
        Assert.Empty(repository.AuditWrites);

        var withReason = await UpdateAsync(service, Edit(), "Added a tabulation");

        Assert.Equal(SchemeUpdateStatus.Updated, withReason.Status);
        var saved = (await repository.GetByIdAsync(SchemeId))!;
        Assert.Equal(3, saved.Tabulations.Count);
        var insert = Assert.Single(repository.LastAudit!.Plan.Tabulations);
        Assert.Equal(SchemeAuditOperation.Insert, insert.Operation);
        Assert.Equal(saved.Tabulations[2].TabulationId, insert.Id);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_ValidationErrorsWinOverTheAuditPrompt()
    {
        var (service, repository) = await StoredSchemeAsync(ClosedYearScheme());
        var updated = ClosedYearEdit("results");
        updated.CustomsDescription = string.Empty;

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => UpdateAsync(service, updated));

        Assert.Contains(exception.Errors, e => e.Field == "CustomsDescription");
        Assert.Empty(repository.AuditWrites);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ValidationErrorsWinOverTheAuditPrompt()
    {
        var (service, repository) = await StoredSchemeAsync();
        var updated = AuditableEdit();
        updated.Name = string.Empty;

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => UpdateAsync(service, updated));

        Assert.Contains(exception.Errors, e => e.Field == "Name");
        Assert.Empty(repository.AuditWrites);
    }

    [Fact]
    public async Task UpdateSchemeAsync_StagedItemsWithEmptyIds_GetTheirIdsBeforeDetectionSoTheAuditMatchesTheSave()
    {
        var (service, repository) = await StoredSchemeAsync();
        var updated = Build();
        updated.Tests.Add(new SchemeTest
        {
            TestTypeId = TypeA,
            MethodItems = [new SchemeTestMethodItem { TestMethodItemTypeId = TypeA }],
            ResultItems = [new SchemeTestResultItem { TestResultItemTypeId = TypeA }],
            Categories =
            [
                new SchemeCategoryItem
                {
                    CategoryItemTypeId = TypeA,
                    Criteria = [new SchemeCriterionItem { CriterionItemTypeId = TypeA }],
                },
            ],
        });
        updated.Tabulations.Add(new SchemeTabulation { Name = "Extra" });

        var result = await UpdateAsync(service, updated, "Added a test");

        Assert.Equal(SchemeUpdateStatus.Updated, result.Status);
        var plan = repository.LastAudit!.Plan;
        var saved = (await repository.GetByIdAsync(SchemeId))!;
        var savedTest = saved.Tests[2];
        Assert.NotEqual(Guid.Empty, savedTest.TestId);
        Assert.Equal(savedTest.TestId, Assert.Single(plan.Tests).Id);
        Assert.Equal(savedTest.MethodItems[0].TestMethodItemId, Assert.Single(plan.TestMethodItems).Id);
        Assert.Equal(savedTest.ResultItems[0].TestResultItemId, Assert.Single(plan.TestResultItems).Id);
        Assert.Equal(savedTest.Categories[0].CategoryItemId, Assert.Single(plan.CategoryItems).Id);
        Assert.Equal(savedTest.Categories[0].Criteria[0].CriterionItemId, Assert.Single(plan.CriterionItems).Id);
        Assert.Equal(saved.Tabulations[2].TabulationId, Assert.Single(plan.Tabulations).Id);
        Assert.NotEqual(Guid.Empty, saved.Tabulations[2].TabulationId);
    }

    [Fact]
    public async Task CreateSchemeAsync_NeverAudits()
    {
        var repository = new FakeSchemeRepository();
        var service = new SchemeService(repository, new FakeLookupRepository(), new FakeViewerRepository(), new FakeParticipantRepository(), NullLogger<SchemeService>.Instance);

        var created = await service.CreateSchemeAsync(Build());

        Assert.Equal(2, created.Tests.Count);
        Assert.Empty(repository.AuditWrites);
    }

    [Fact]
    public async Task RenewSchemeAsync_ThenCreatingTheDraft_NeverAudits()
    {
        var (service, repository) = await StoredSchemeAsync();

        var draft = await service.RenewSchemeAsync(SchemeId);
        var created = await service.CreateSchemeAsync(draft!);

        Assert.Equal(2, created.Tests.Count);
        Assert.Empty(repository.AuditWrites);
    }
}
